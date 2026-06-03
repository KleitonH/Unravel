using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;
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

        var enqueued = await queue.EnqueueForContentAsync(
            contentId, claims,
            urgent ? ForgeJobPriority.Urgent : ForgeJobPriority.Normal,
            ct);

        return Ok(new { contentId, contentTitle = content.Title, claimsCandidates = claims.Count, enqueued });
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
