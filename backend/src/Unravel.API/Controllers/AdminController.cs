using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey;
using Unravel.Application.Knowledge.Ports;
using Unravel.Application.Tokens.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Tokens;
using Unravel.Infrastructure.Knowledge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.API.Controllers;

/// <summary>
/// Operações administrativas — protegidas por role Moderator.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Moderator")]
public sealed class AdminController(
    DailyReplanService replan,
    KnowledgeImporter  knowledgeImporter,
    IConfiguration     configuration,
    IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Roda o lote de replanejamento <i>agora</i>. Idempotente:
    /// se já rodou hoje, faz upsert dos snapshots (não duplica).
    /// Resposta inclui o relatório do lote.</summary>
    [HttpPost("replan-now")]
    public async Task<IActionResult> ReplanNow(CancellationToken ct)
    {
        var report = await replan.RunAsync(DateTime.UtcNow, ct);
        return Ok(report);
    }

    /// <summary>
    /// PR 28 — re-importa todas as trilhas em <c>backend/knowledge/</c>.
    /// Idempotente (upsert por slug). Útil quando adicionar/editar MDs
    /// sem precisar reiniciar a API.
    /// </summary>
    [HttpPost("knowledge/import")]
    public async Task<IActionResult> ImportKnowledge(CancellationToken ct)
    {
        var configured = configuration["Knowledge:Path"];
        var rootPath   = !string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured ?? "../../knowledge"));

        var summary = await knowledgeImporter.ImportAllAsync(rootPath, ct);
        return Ok(new
        {
            rootPath,
            summary.TrailsCreated,
            summary.TrailsUpdated,
            summary.ContentsCreated,
            summary.ContentsUpdated,
        });
    }

    /// <summary>
    /// PR 32 — enfileira jobs de geração LLM-grounded pra um Content.
    /// Extrai claims do conteúdo e cria 1 job por claim (até <c>max</c>).
    /// Worker BackgroundService processa em batch (1 por vez na GPU).
    /// </summary>
    [HttpPost("forge/{contentId:int}")]
    public async Task<IActionResult> EnqueueForge(
        int contentId,
        [FromQuery] int max = 20,
        [FromQuery] bool urgent = false,
        [FromServices] ApplicationDbContext db = null!,
        [FromServices] IClaimExtractor extractor = null!,
        [FromServices] IQuestionForgeQueue queue = null!,
        [FromServices] IModeratorTokenService tokens = null!,
        CancellationToken ct = default)
    {
        var content = await db.Content.Where(c => c.Id == contentId)
            .Select(c => new { c.Id, c.Title, c.Body })
            .FirstOrDefaultAsync(ct);
        if (content is null) return NotFound(new { message = $"Content {contentId} não existe." });

        var claims = extractor.Extract(content.Body)
            .OrderByDescending(c => c.Score)
            .Take(max)
            .ToList();

        if (claims.Count == 0)
            return Ok(new { contentId, contentTitle = content.Title, enqueued = 0, message = "Nenhuma claim extratível desse conteúdo." });

        // PR 52 — debita tokens (lã) ANTES de enqueue. Custo varia por urgent.
        var costPerJob = urgent ? 3 : 1;
        var totalCost  = claims.Count * costPerJob;
        try
        {
            await tokens.DebitAsync(UserId(), totalCost,
                urgent ? TokenTransactionReason.ForgeUrgent : TokenTransactionReason.ForgeNormal,
                metadata: System.Text.Json.JsonSerializer.Serialize(new {
                    contentId, contentTitle = content.Title, jobs = claims.Count, urgent
                }), ct: ct);
        }
        catch (InsufficientTokensException ex)
        {
            return StatusCode(402, new {
                message = ex.Message,
                balanceCm = ex.BalanceCm,
                requiredCm = ex.RequiredCm,
            });
        }

        // PR 52a — agrupa esse enqueue num batch identificável pra UI
        // de progresso do moderador.
        var batchId = Guid.NewGuid();
        var enqueued = await queue.EnqueueForContentAsync(
            contentId, claims,
            urgent ? ForgeJobPriority.Urgent : ForgeJobPriority.Normal,
            batchId: batchId,
            enqueuedByUserId: UserId(),
            ct: ct);

        return Ok(new {
            contentId, contentTitle = content.Title,
            claimsCandidates = claims.Count, enqueued,
            tokensSpentCm = totalCost,
            batchId,
        });
    }

    /// <summary>
    /// PR 32 — snapshot da fila pra dashboard admin / monitoring.
    /// </summary>
    [HttpGet("forge/status")]
    public async Task<IActionResult> ForgeStatus(
        [FromServices] IQuestionForgeQueue queue,
        CancellationToken ct)
    {
        var status = await queue.GetStatusAsync(ct);
        return Ok(status);
    }

    /// <summary>
    /// PR 52a — status agregado + jobs detalhados de um batch.
    /// Permite ao moderador acompanhar o progresso dos jobs que ELE
    /// disparou (sem misturar com fila global de outros admins/cron).
    ///
    /// <para>Retorna 404 se batch não pertence ao moderador autenticado —
    /// privacidade básica (admin não vê batches de outro admin).</para>
    /// </summary>
    [HttpGet("forge/batches/{batchId:guid}")]
    public async Task<IActionResult> GetBatch(
        Guid batchId,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var userId = UserId();
        var jobs = await db.QuestionForgeJob.AsNoTracking()
            .Where(j => j.BatchId == batchId && j.EnqueuedByUserId == userId)
            .OrderBy(j => j.Id)
            .ToListAsync(ct);

        if (jobs.Count == 0)
            return NotFound(new { message = "Batch não encontrado ou não pertence a você." });

        // Recupera prompts/shapes das GeneratedChallenges criadas
        // (necessário pra UI mostrar "essa pergunta saiu como FillBlank").
        var challengeIds = jobs.Where(j => j.GeneratedChallengeId.HasValue)
                               .Select(j => j.GeneratedChallengeId!.Value)
                               .ToList();
        var challenges = challengeIds.Count == 0
            ? new Dictionary<int, GeneratedChallenge>()
            : await db.GeneratedChallenge.AsNoTracking()
                .Where(g => challengeIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, ct);

        // Resolve título do Content (1 query — todos os jobs do batch
        // costumam pertencer a poucos contents distintos).
        var contentIds = jobs.Select(j => j.ContentId).Distinct().ToList();
        var contents = await db.Content.AsNoTracking()
            .Where(c => contentIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Title, ct);

        var jobDtos = jobs.Select(j => new {
            id              = j.Id,
            contentId       = j.ContentId,
            contentTitle    = contents.GetValueOrDefault(j.ContentId, "(removido)"),
            status          = j.Status.ToString(),
            priority        = j.Priority.ToString(),
            enqueuedAt      = j.EnqueuedAt,
            startedAt       = j.StartedAt,
            completedAt     = j.CompletedAt,
            attemptCount    = j.AttemptCount,
            lastError       = j.LastError,
            claimText       = j.ClaimText,
            generatedChallengeId = j.GeneratedChallengeId,
            // Shape vem do BodyJson da challenge associada
            shape = j.GeneratedChallengeId.HasValue
                && challenges.TryGetValue(j.GeneratedChallengeId.Value, out var ch)
                ? ExtractShape(ch.BodyJson)
                : null,
            prompt = j.GeneratedChallengeId.HasValue
                && challenges.TryGetValue(j.GeneratedChallengeId.Value, out var ch2)
                ? ch2.Prompt
                : null,
        }).ToList();

        var counts = new {
            pending = jobs.Count(j => j.Status == ForgeJobStatus.Pending),
            running = jobs.Count(j => j.Status == ForgeJobStatus.Running),
            done    = jobs.Count(j => j.Status == ForgeJobStatus.Done),
            failed  = jobs.Count(j => j.Status == ForgeJobStatus.Failed),
        };

        // Shape breakdown sobre os Done (válidos)
        var shapeBreakdown = jobDtos
            .Where(d => d.shape != null)
            .GroupBy(d => d.shape!)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(new {
            batchId,
            total       = jobs.Count,
            enqueuedAt  = jobs.Min(j => j.EnqueuedAt),
            isComplete  = counts.pending == 0 && counts.running == 0,
            counts,
            shapeBreakdown,
            jobs        = jobDtos,
        });
    }

    /// <summary>
    /// PR 52a — lista os últimos N batches do moderador autenticado
    /// (resumo apenas, sem jobs detalhados). Endpoint pro drawer/chip
    /// de "Atividade do Forge" no header admin.
    /// </summary>
    [HttpGet("forge/batches/recent")]
    public async Task<IActionResult> RecentBatches(
        [FromServices] ApplicationDbContext db,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        var userId = UserId();
        take = Math.Clamp(take, 1, 50);

        // Agrega por batchId. Postgres EF traduz GROUP BY direto.
        var batches = await db.QuestionForgeJob.AsNoTracking()
            .Where(j => j.EnqueuedByUserId == userId && j.BatchId != null)
            .GroupBy(j => j.BatchId!.Value)
            .Select(g => new {
                batchId    = g.Key,
                total      = g.Count(),
                pending    = g.Count(j => j.Status == ForgeJobStatus.Pending),
                running    = g.Count(j => j.Status == ForgeJobStatus.Running),
                done       = g.Count(j => j.Status == ForgeJobStatus.Done),
                failed     = g.Count(j => j.Status == ForgeJobStatus.Failed),
                enqueuedAt = g.Min(j => j.EnqueuedAt),
                lastUpdate = g.Max(j => j.CompletedAt ?? j.StartedAt ?? j.EnqueuedAt),
            })
            .OrderByDescending(b => b.enqueuedAt)
            .Take(take)
            .ToListAsync(ct);

        return Ok(batches);
    }

    /// <summary>
    /// PR 34d — stats agregados de todo o forge do moderador autenticado.
    /// Alimenta a página <c>/admin/forge</c> com cards (total batches/jobs,
    /// yield, breakdown por shape) e tabela de top motivos de falha.
    ///
    /// <para>Filtra por <see cref="UserId"/> — moderador não vê stats dos
    /// outros. Inclui jobs sem BatchId? Não — só agregar com batch garante
    /// que o número bate com o que aparece na "Atividade do Forge".</para>
    /// </summary>
    [HttpGet("forge/stats")]
    public async Task<IActionResult> ForgeStats(
        [FromServices] ApplicationDbContext db,
        CancellationToken ct = default)
    {
        var userId = UserId();

        // Jobs do moderador (todos os batches)
        var myJobs = db.QuestionForgeJob.AsNoTracking()
            .Where(j => j.EnqueuedByUserId == userId && j.BatchId != null);

        var totalJobs   = await myJobs.CountAsync(ct);
        var doneJobs    = await myJobs.CountAsync(j => j.Status == ForgeJobStatus.Done, ct);
        var failedJobs  = await myJobs.CountAsync(j => j.Status == ForgeJobStatus.Failed, ct);
        var pendingJobs = await myJobs.CountAsync(j => j.Status == ForgeJobStatus.Pending, ct);
        var runningJobs = await myJobs.CountAsync(j => j.Status == ForgeJobStatus.Running, ct);

        var totalBatches = await myJobs.Select(j => j.BatchId).Distinct().CountAsync(ct);

        // Top 5 motivos de falha (LastError truncado pra primeira palavra/tag
        // pra agrupar erros como "LlmEmpty: ..." e "DistractorsPoor: ..." por
        // categoria, ignorando detalhe específico do job).
        var failedWithReason = await myJobs
            .Where(j => j.Status == ForgeJobStatus.Failed && j.LastError != null)
            .Select(j => j.LastError!)
            .ToListAsync(ct);
        var topFailureReasons = failedWithReason
            .Select(NormalizeFailureReason)
            .GroupBy(r => r)
            .Select(g => new { reason = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(5)
            .ToList();

        // Shape breakdown (apenas Done — só contam válidas).
        // Recupera GeneratedChallenge.BodyJson e extrai shape.
        var doneChallengeIds = await myJobs
            .Where(j => j.Status == ForgeJobStatus.Done && j.GeneratedChallengeId != null)
            .Select(j => j.GeneratedChallengeId!.Value)
            .ToListAsync(ct);
        var bodies = doneChallengeIds.Count == 0
            ? new List<string>()
            : await db.GeneratedChallenge.AsNoTracking()
                .Where(g => doneChallengeIds.Contains(g.Id))
                .Select(g => g.BodyJson)
                .ToListAsync(ct);

        var shapeCounts = bodies
            .Select(ExtractShape)
            .Select(s => s ?? "MultipleChoice")
            .GroupBy(s => s)
            .ToDictionary(g => g.Key, g => g.Count());

        var yieldRate = (doneJobs + failedJobs) == 0
            ? (double?)null
            : (double)doneJobs / (doneJobs + failedJobs);

        return Ok(new {
            totalBatches,
            totalJobs,
            doneJobs,
            failedJobs,
            pendingJobs,
            runningJobs,
            yieldRate,
            shapeCounts,
            topFailureReasons,
        });
    }

    /// <summary>Normaliza um <c>LastError</c> num "bucket" estável pra
    /// agrupar falhas no top reasons. Ex:
    /// <c>"LlmEmpty: LLM returned empty"</c> → <c>"LlmEmpty"</c>.
    /// <c>"DistractorsPoor: Apenas 1/3..."</c> → <c>"DistractorsPoor"</c>.</summary>
    private static string NormalizeFailureReason(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
        var idx = raw.IndexOf(':');
        return idx > 0 ? raw[..idx].Trim() : raw[..Math.Min(60, raw.Length)];
    }

    /// <summary>Helper pra extrair "shape" do BodyJson da
    /// <c>GeneratedChallenge</c>. Retorna null se ausente (rows pré-PR-34a).</summary>
    private static string? ExtractShape(string bodyJson)
    {
        if (string.IsNullOrWhiteSpace(bodyJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(bodyJson);
            return doc.RootElement.TryGetProperty("shape", out var s)
                ? s.GetString()
                : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// PR 33h — bulk: enfileira jobs pra TODOS os Contents ativos de uma
    /// trilha (ou de todas, se trailSlug não passado).
    /// Útil pra encher o pool de uma vez só, em vez de chamar
    /// POST forge/{contentId} N vezes manualmente.
    /// </summary>
    [HttpPost("forge/bulk")]
    public async Task<IActionResult> EnqueueForgeBulk(
        [FromQuery] string? trailSlug = null,
        [FromQuery] int max = 20,
        [FromQuery] bool urgent = false,
        [FromServices] ApplicationDbContext db = null!,
        [FromServices] IClaimExtractor extractor = null!,
        [FromServices] IQuestionForgeQueue queue = null!,
        [FromServices] IModeratorTokenService tokens = null!,
        CancellationToken ct = default)
    {
        // Filtra Contents da trilha (ou todos, se sem filtro)
        var contentsQuery = db.Content.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(trailSlug))
            contentsQuery = contentsQuery.Where(c => c.Trail.Slug == trailSlug);

        var contents = await contentsQuery
            .Select(c => new { c.Id, c.Title, c.Body })
            .ToListAsync(ct);

        if (contents.Count == 0)
            return NotFound(new { message = trailSlug is null
                ? "Nenhum Content ativo no DB."
                : $"Nenhum Content ativo na trilha '{trailSlug}'." });

        var perContent  = new List<object>(contents.Count);
        var totalQueued = 0;

        // PR 52 — pré-calcula custo total e debita ANTES de enqueue.
        // Bulk falha atômico: se moderador não tem tokens pra cobrir tudo,
        // nenhum job é enfileirado (em vez de gerar parcial e gastar lã à toa).
        var preClaimsByContent = contents.ToDictionary(c => c.Id,
            c => extractor.Extract(c.Body).OrderByDescending(x => x.Score).Take(max).ToList());
        var totalJobsExpected = preClaimsByContent.Values.Sum(l => l.Count);
        var costPerJob = urgent ? 3 : 1;
        var totalCost  = totalJobsExpected * costPerJob;

        if (totalJobsExpected == 0)
            return Ok(new { trailSlug, maxPerContent = max, urgent,
                totalContents = contents.Count, totalQueued = 0,
                message = "Nenhuma claim extratível nos conteúdos." });

        try
        {
            await tokens.DebitAsync(UserId(), totalCost,
                urgent ? TokenTransactionReason.ForgeUrgent : TokenTransactionReason.ForgeNormal,
                metadata: System.Text.Json.JsonSerializer.Serialize(new {
                    trailSlug, contents = contents.Count, jobs = totalJobsExpected, urgent
                }), ct: ct);
        }
        catch (InsufficientTokensException ex)
        {
            return StatusCode(402, new {
                message = ex.Message,
                balanceCm = ex.BalanceCm,
                requiredCm = ex.RequiredCm,
            });
        }

        // PR 52a — UM batch cobre todos os Contents desse bulk. Permite
        // UI mostrar progresso global ("bulk PHP: 12/30 jobs feitos")
        // sem fragmentar por content.
        var batchId = Guid.NewGuid();
        var userId  = UserId();

        foreach (var content in contents)
        {
            ct.ThrowIfCancellationRequested();
            var claims = preClaimsByContent[content.Id];

            if (claims.Count == 0)
            {
                perContent.Add(new { contentId = content.Id, contentTitle = content.Title,
                    claimsCandidates = 0, enqueued = 0 });
                continue;
            }

            var enqueued = await queue.EnqueueForContentAsync(
                content.Id, claims,
                urgent ? ForgeJobPriority.Urgent : ForgeJobPriority.Normal,
                batchId: batchId,
                enqueuedByUserId: userId,
                ct: ct);
            totalQueued += enqueued;
            perContent.Add(new { contentId = content.Id, contentTitle = content.Title,
                claimsCandidates = claims.Count, enqueued });
        }

        return Ok(new
        {
            trailSlug,
            maxPerContent = max,
            urgent,
            totalContents = contents.Count,
            totalQueued,
            tokensSpentCm = totalCost,
            batchId,
            perContent,
        });
    }

    // ─── PR 33d — Moderator-curated gold ────────────────────────────

    /// <summary>Lista gold curado por moderador pra um Content.</summary>
    [HttpGet("gold/{contentId:int}")]
    public async Task<IActionResult> ListGold(
        int contentId,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var items = await db.ModeratorGoldItem
            .AsNoTracking()
            .Where(g => g.ContentId == contentId && g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GoldDto(
                g.Id,
                g.SourceGeneratedChallengeId,
                g.SourceClaim,
                g.Prompt,
                g.CorrectAnswer,
                g.DistractorsJson,
                g.Explanation,
                g.DifficultyHint,
                g.CreatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>
    /// Cria item de gold pro Content. Dois modos:
    /// <list type="bullet">
    ///   <item><b>Promover gerada</b>: passe <c>sourceGeneratedChallengeId</c>
    ///   — o backend copia prompt/options/correctAnswer da pergunta gerada.</item>
    ///   <item><b>Manual</b>: passe todos os campos — backend valida 3 distratores.</item>
    /// </list>
    /// </summary>
    [HttpPost("gold/{contentId:int}")]
    public async Task<IActionResult> AddGold(
        int contentId,
        [FromBody] AddGoldRequest dto,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var content = await db.Content.FindAsync(new object[] { contentId }, ct);
        if (content is null) return NotFound(new { message = $"Content {contentId} não existe." });

        var curatorId = UserId();

        var item = new ModeratorGoldItem
        {
            ContentId    = contentId,
            CuratorUserId = curatorId,
            CreatedAt    = DateTime.UtcNow,
            IsActive     = true,
        };

        if (dto.SourceGeneratedChallengeId is { } gcId)
        {
            // Modo "promover gerada" — copia campos
            var gc = await db.GeneratedChallenge.FindAsync(new object[] { gcId }, ct);
            if (gc is null) return NotFound(new { message = $"GeneratedChallenge {gcId} não existe." });
            if (gc.ContentId != contentId)
                return BadRequest(new { message = "GeneratedChallenge não pertence ao Content informado." });

            // Re-parse do BodyJson pra extrair options/correctIndex/explanation
            using var parsed = JsonDocument.Parse(gc.BodyJson);
            var root = parsed.RootElement;
            var options = root.GetProperty("options").EnumerateArray()
                .Select(o => o.GetString() ?? "").ToList();
            var correctIdx = root.GetProperty("correctIndex").GetInt32();
            string? explanation = root.TryGetProperty("explanation", out var expEl)
                ? expEl.GetString() : null;

            if (options.Count != 4) return BadRequest(new { message = "Generated challenge não tem exatamente 4 options." });
            if (correctIdx < 0 || correctIdx >= 4) return BadRequest(new { message = "correctIndex inválido." });

            var distractors = options.Where((_, i) => i != correctIdx).ToList();

            item.SourceGeneratedChallengeId = gcId;
            item.SourceClaim       = dto.SourceClaim;   // opcional, moderador pode anexar
            item.Prompt            = gc.Prompt;
            item.CorrectAnswer     = options[correctIdx];
            item.DistractorsJson   = JsonSerializer.Serialize(distractors);
            item.Explanation       = explanation;
            item.DifficultyHint    = dto.DifficultyHint ?? gc.EstimatedDifficulty;
        }
        else
        {
            // Modo manual — exige tudo
            if (string.IsNullOrWhiteSpace(dto.Prompt))
                return BadRequest(new { message = "Prompt obrigatório em modo manual." });
            if (string.IsNullOrWhiteSpace(dto.CorrectAnswer))
                return BadRequest(new { message = "CorrectAnswer obrigatório em modo manual." });
            if (dto.Distractors is null || dto.Distractors.Count != 3
                || dto.Distractors.Any(string.IsNullOrWhiteSpace))
                return BadRequest(new { message = "Distractors deve ter exatamente 3 strings não-vazias." });

            item.Prompt          = dto.Prompt;
            item.CorrectAnswer   = dto.CorrectAnswer;
            item.DistractorsJson = JsonSerializer.Serialize(dto.Distractors);
            item.Explanation     = dto.Explanation;
            item.SourceClaim     = dto.SourceClaim;
            item.DifficultyHint  = dto.DifficultyHint;
        }

        db.ModeratorGoldItem.Add(item);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ListGold), new { contentId }, new { item.Id });
    }

    /// <summary>Soft-delete: marca item como inativo. Histórico fica.</summary>
    [HttpDelete("gold/{goldId:int}")]
    public async Task<IActionResult> RemoveGold(
        int goldId,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var item = await db.ModeratorGoldItem.FindAsync(new object[] { goldId }, ct);
        if (item is null) return NotFound();
        item.IsActive  = false;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ─── PR 35 — Trilhas custom de moderador ────────────────────────

    /// <summary>
    /// Lista trilhas custom do moderador autenticado. Trilhas Git são
    /// excluídas (gerenciadas via filesystem, não via API).
    /// </summary>
    [HttpGet("trails")]
    public async Task<IActionResult> ListMyTrails(
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var ownerId = UserId();
        var trails = await db.Trail
            .AsNoTracking()
            .Where(t => t.Source == ContentSource.ModeratorCustom && t.OwnerUserId == ownerId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new CustomTrailDto(
                t.Id, t.Slug, t.Name, t.Description, t.Icon, t.AccentColor,
                (int)t.Level, t.IsActive, t.IsPublished, t.CreatedAt,
                t.Contents.Count(c => c.IsActive)))
            .ToListAsync(ct);
        return Ok(trails);
    }

    /// <summary>
    /// Cria trilha custom. Slug é opcional; se omitido, gera a partir
    /// do nome (lowercase + hifens). Slug deve ser globalmente único.
    /// </summary>
    [HttpPost("trails")]
    public async Task<IActionResult> CreateTrail(
        [FromBody] CreateCustomTrailRequest dto,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Name é obrigatório." });

        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? Slugify(dto.Name) : dto.Slug.Trim().ToLowerInvariant();
        if (await db.Trail.AnyAsync(t => t.Slug == slug, ct))
            return Conflict(new { message = $"Slug '{slug}' já está em uso." });

        var trail = new Trail
        {
            Slug        = slug,
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Icon        = string.IsNullOrWhiteSpace(dto.Icon)        ? "📘"      : dto.Icon.Trim(),
            AccentColor = string.IsNullOrWhiteSpace(dto.AccentColor) ? "#7038f2" : dto.AccentColor.Trim(),
            Level       = ParseLevel(dto.Level),
            Source      = ContentSource.ModeratorCustom,
            OwnerUserId = UserId(),
            IsActive    = true,
            IsPublished = false,  // rascunho por default — moderador publica explicitamente
            CreatedAt   = DateTime.UtcNow,
        };
        db.Trail.Add(trail);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListMyTrails), null,
            new CustomTrailDto(trail.Id, trail.Slug, trail.Name, trail.Description, trail.Icon,
                trail.AccentColor, (int)trail.Level, trail.IsActive, trail.IsPublished,
                trail.CreatedAt, 0));
    }

    /// <summary>Edita metadados de trilha custom. Bloqueia edição de trilhas Git.</summary>
    [HttpPatch("trails/{trailId:int}")]
    public async Task<IActionResult> UpdateTrail(
        int trailId,
        [FromBody] UpdateCustomTrailRequest dto,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var trail = await db.Trail.FindAsync(new object[] { trailId }, ct);
        if (trail is null) return NotFound();
        if (trail.Source != ContentSource.ModeratorCustom)
            return Forbid();
        if (trail.OwnerUserId != UserId())
            return Forbid();

        if (dto.Name is not null) trail.Name = dto.Name.Trim();
        if (dto.Description is not null) trail.Description = dto.Description.Trim();
        if (dto.Icon is not null) trail.Icon = dto.Icon.Trim();
        if (dto.AccentColor is not null) trail.AccentColor = dto.AccentColor.Trim();
        if (dto.Level is not null) trail.Level = ParseLevel(dto.Level);
        if (dto.IsPublished is bool pub) trail.IsPublished = pub;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Soft-delete (IsActive=false). Trilha desaparece pros alunos
    /// mas perguntas/respostas históricas ficam.</summary>
    [HttpDelete("trails/{trailId:int}")]
    public async Task<IActionResult> DeleteTrail(
        int trailId,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var trail = await db.Trail.FindAsync(new object[] { trailId }, ct);
        if (trail is null) return NotFound();
        if (trail.Source != ContentSource.ModeratorCustom) return Forbid();
        if (trail.OwnerUserId != UserId()) return Forbid();

        trail.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ─── PR 35 — Contents custom de moderador ───────────────────────

    /// <summary>
    /// Lista contents de uma trilha (custom). Inclui body completo —
    /// pra editor markdown carregar.
    /// </summary>
    [HttpGet("trails/{trailId:int}/contents")]
    public async Task<IActionResult> ListContents(
        int trailId,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var trail = await db.Trail
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trailId, ct);
        if (trail is null) return NotFound();
        if (trail.Source != ContentSource.ModeratorCustom) return Forbid();
        if (trail.OwnerUserId != UserId()) return Forbid();

        var contents = await db.Content
            .AsNoTracking()
            .Where(c => c.TrailId == trailId)
            .OrderBy(c => c.Order).ThenBy(c => c.Id)
            .Select(c => new CustomContentDto(
                c.Id, c.Slug, c.Title, c.Body, c.Order,
                (int)c.Level, c.IsActive, c.CreatedAt, c.EditedAt))
            .ToListAsync(ct);
        return Ok(contents);
    }

    /// <summary>
    /// Cria content custom dentro de trilha custom. Body é markdown raw;
    /// chunks/claims são extraídos on-the-fly no momento de gerar perguntas
    /// (não precisa de processing prévio).
    /// </summary>
    [HttpPost("trails/{trailId:int}/contents")]
    public async Task<IActionResult> CreateContent(
        int trailId,
        [FromBody] CreateCustomContentRequest dto,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var trail = await db.Trail.FindAsync(new object[] { trailId }, ct);
        if (trail is null) return NotFound(new { message = $"Trail {trailId} não existe." });
        if (trail.Source != ContentSource.ModeratorCustom) return Forbid();
        if (trail.OwnerUserId != UserId()) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title é obrigatório." });
        if (string.IsNullOrWhiteSpace(dto.Body))
            return BadRequest(new { message = "Body (markdown) é obrigatório." });

        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? Slugify(dto.Title) : dto.Slug.Trim().ToLowerInvariant();
        if (await db.Content.AnyAsync(c => c.Slug == slug, ct))
            return Conflict(new { message = $"Slug '{slug}' já está em uso (slug é único globalmente)." });

        // Order: se não passado, vai pro fim
        var order = dto.Order ?? await db.Content
            .Where(c => c.TrailId == trailId)
            .Select(c => (int?)c.Order)
            .MaxAsync(ct) ?? 0;
        if (dto.Order is null) order += 1;

        var content = new Content
        {
            Slug      = slug,
            Title     = dto.Title.Trim(),
            Body      = dto.Body,  // markdown raw, não trim — preserva indentação de code blocks
            TrailId   = trailId,
            Order     = order,
            Level     = ParseLevel(dto.Level),
            Type      = ContentType.Article,
            Source    = ContentSource.ModeratorCustom,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Content.Add(content);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListContents), new { trailId },
            new CustomContentDto(content.Id, content.Slug, content.Title, content.Body,
                content.Order, (int)content.Level, content.IsActive, content.CreatedAt, null));
    }

    /// <summary>
    /// Edita content custom. Se o body mudou, marca perguntas existentes
    /// como <c>IsActive=false</c> (reset conservador — moderador roda
    /// forge bulk de novo pra repopular). Versão futura: hash-diff por
    /// chunk preservando perguntas de chunks inalterados.
    /// </summary>
    [HttpPatch("contents/{contentId:int}")]
    public async Task<IActionResult> UpdateContent(
        int contentId,
        [FromBody] UpdateCustomContentRequest dto,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var content = await db.Content.FirstOrDefaultAsync(c => c.Id == contentId, ct);
        if (content is null) return NotFound();
        if (content.Source != ContentSource.ModeratorCustom) return Forbid();

        var trail = await db.Trail.FindAsync(new object[] { content.TrailId }, ct);
        if (trail?.OwnerUserId != UserId()) return Forbid();

        var bodyChanged = dto.Body is not null && dto.Body != content.Body;

        if (dto.Title is not null) content.Title = dto.Title.Trim();
        if (dto.Body  is not null) content.Body  = dto.Body;
        if (dto.Order is int ord)  content.Order = ord;
        if (dto.Level is not null) content.Level = ParseLevel(dto.Level);
        if (dto.IsActive is bool act) content.IsActive = act;

        if (bodyChanged)
        {
            content.EditedAt       = DateTime.UtcNow;
            content.EditedByUserId = UserId();

            // Reset conservador: invalida pool. Moderador roda
            // POST /forge/{contentId} pra repopular. Garante que aluno
            // não veja pergunta gerada contra texto antigo.
            await db.GeneratedChallenge
                .Where(gc => gc.ContentId == contentId && gc.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(gc => gc.IsActive, false), ct);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Soft-delete de content custom.</summary>
    [HttpDelete("contents/{contentId:int}")]
    public async Task<IActionResult> DeleteContent(
        int contentId,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        var content = await db.Content.FirstOrDefaultAsync(c => c.Id == contentId, ct);
        if (content is null) return NotFound();
        if (content.Source != ContentSource.ModeratorCustom) return Forbid();

        var trail = await db.Trail.FindAsync(new object[] { content.TrailId }, ct);
        if (trail?.OwnerUserId != UserId()) return Forbid();

        content.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ─── Helpers ────────────────────────────────────────────────────

    /// <summary>Slugify simples: lowercase + remove diacríticos + colapsa
    /// não-alfanuméricos em hífen. "Banco de Dados Avançado" → "banco-de-dados-avancado".</summary>
    private static string Slugify(string s)
    {
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private static DifficultyLevel ParseLevel(string? level) =>
        (level ?? "Beginner").Trim().ToLowerInvariant() switch
        {
            "beginner"     or "iniciante"     => DifficultyLevel.Beginner,
            "intermediate" or "intermediario" or "intermediário" => DifficultyLevel.Intermediate,
            "advanced"     or "avancado"      or "avançado"      => DifficultyLevel.Advanced,
            _ => DifficultyLevel.Beginner,
        };

    private Guid UserId() => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

// ── DTOs ────────────────────────────────────────────────────────────

public record AddGoldRequest(
    int?          SourceGeneratedChallengeId,  // null = modo manual
    string?       SourceClaim,
    string?       Prompt,                       // obrig se manual
    string?       CorrectAnswer,                // obrig se manual
    List<string>? Distractors,                  // obrig se manual (3 itens)
    string?       Explanation,
    double?       DifficultyHint);

public record GoldDto(
    int       Id,
    int?      SourceGeneratedChallengeId,
    string?   SourceClaim,
    string    Prompt,
    string    CorrectAnswer,
    string    DistractorsJson,
    string?   Explanation,
    double?   DifficultyHint,
    DateTime  CreatedAt);

// PR 35 — Trail/Content custom DTOs

public record CreateCustomTrailRequest(
    string  Name,
    string? Slug,
    string? Description,
    string? Icon,
    string? AccentColor,
    string? Level);

public record UpdateCustomTrailRequest(
    string? Name,
    string? Description,
    string? Icon,
    string? AccentColor,
    string? Level,
    bool?   IsPublished);

public record CustomTrailDto(
    int      Id,
    string?  Slug,
    string   Name,
    string   Description,
    string   Icon,
    string   AccentColor,
    int      Level,
    bool     IsActive,
    bool     IsPublished,
    DateTime CreatedAt,
    int      ContentsCount);

public record CreateCustomContentRequest(
    string  Title,
    string  Body,
    string? Slug,
    int?    Order,
    string? Level);

public record UpdateCustomContentRequest(
    string? Title,
    string? Body,
    int?    Order,
    string? Level,
    bool?   IsActive);

public record CustomContentDto(
    int       Id,
    string?   Slug,
    string    Title,
    string    Body,
    int       Order,
    int       Level,
    bool      IsActive,
    DateTime  CreatedAt,
    DateTime? EditedAt);
