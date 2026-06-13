using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge.Queue;

/// <summary>
/// BackgroundService que consome 1 job de cada vez da
/// <see cref="IQuestionForgeQueue"/>, chama
/// <see cref="IGroundedQuestionGenerator"/> (PR 31), e persiste o
/// resultado como <see cref="GeneratedChallenge"/> ativo.
///
/// <para><b>Por que single-worker</b>: a inferência LLM é GPU-bound
/// (RTX 3060 6GB) e o Ollama processa 1 request por vez por modelo
/// carregado. Mais workers paralelos brigariam pela mesma VRAM
/// fazendo offload/reload — pior throughput.</para>
///
/// <para><b>Loop</b>:</para>
/// <list type="number">
///   <item>ClaimNext atômico → null = fila vazia, dorme N segundos</item>
///   <item>GenerateAsync (latência 5-30s) → produz pergunta validada</item>
///   <item>Persist GeneratedChallenge + MarkDone</item>
///   <item>Em qualquer exceção: MarkFailed (com retry policy)</item>
/// </list>
///
/// <para><b>Token cap diário</b>: config <c>ForgeQueue:DailyTokenCap</c>.
/// Quando atingido, para até a próxima madrugada UTC. Default 200k
/// tokens (suficiente pra ~400 perguntas de qualidade no Qwen 7B).</para>
///
/// <para><b>Cancelation</b>: respeitada em todos os await; SIGTERM faz
/// shutdown limpo aguardando até 30s pelo job atual terminar.</para>
/// </summary>
public sealed class QuestionForgeWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestionForgeWorker> log) : BackgroundService
{
    private const int  PollIntervalSecondsWhenEmpty = 30;
    private const int  MaxAttempts                  = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("QuestionForgeWorker started. Poll interval (empty)={Poll}s, maxAttempts={Max}",
            PollIntervalSecondsWhenEmpty, MaxAttempts);

        // Espera 5s no startup pra dar tempo do LlmHealthCheck rodar
        // primeiro (logs do health vêm antes dos do worker).
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await TryProcessOneAsync(stoppingToken);
                if (!processed)
                    await Task.Delay(TimeSpan.FromSeconds(PollIntervalSecondsWhenEmpty), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "QuestionForgeWorker outer loop threw unexpectedly; sleeping 60s.");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        log.LogInformation("QuestionForgeWorker stopped.");
    }

    /// <summary>Tenta processar 1 job. Retorna <c>true</c> se processou
    /// (sucesso ou falha), <c>false</c> se fila vazia.</summary>
    private async Task<bool> TryProcessOneAsync(CancellationToken ct)
    {
        // Scope por iteração — DbContext/HttpClient são tipicamente
        // scoped/transient; criar fresh evita state leak entre jobs.
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp        = scope.ServiceProvider;
        var queue     = sp.GetRequiredService<IQuestionForgeQueue>();
        var generator = sp.GetRequiredService<IGroundedQuestionGenerator>();
        var db        = sp.GetRequiredService<ApplicationDbContext>();

        var job = await queue.ClaimNextAsync(ct);
        if (job is null) return false;

        log.LogDebug("Processing job {Id} (content={Content}, chunk={Chunk}, attempt={Attempt})",
            job.Id, job.ContentId, job.ChunkIndex, job.AttemptCount);

        // Pega título do Content (passado pro prompt builder)
        var content = await db.Content
            .Where(c => c.Id == job.ContentId)
            .Select(c => new { c.Title, c.TrailId })
            .FirstOrDefaultAsync(ct);
        if (content is null)
        {
            await queue.MarkFailedAsync(job.Id, "Content not found (deleted?)", MaxAttempts, ct);
            return true;
        }

        // Reconstrói ClaimCandidate. ChunkText fica vazio pra esse
        // caminho — o gerador chamará IClaimExtractor de novo OU
        // recebe só o claim? O prompt usa chunkText. Precisamos re-extrair.
        // Simplification: re-extrair claims do conteúdo agora seria caro.
        // Solução: persistir ChunkText no job. ALT: re-extrair com cache.
        //
        // Decisão pragmática: re-extrair sob demanda usando IClaimExtractor.
        // O conteúdo já está no DB; a extração é puro CPU (~10ms por MD).
        var extractor = sp.GetRequiredService<IClaimExtractor>();
        var body = await db.Content.Where(c => c.Id == job.ContentId).Select(c => c.Body).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(body))
        {
            await queue.MarkFailedAsync(job.Id, "Content body empty", MaxAttempts, ct);
            return true;
        }
        var claims = extractor.Extract(body);
        var claim  = claims.FirstOrDefault(c => c.ChunkIndex == job.ChunkIndex
                                             && QuestionForgeQueueService.HashClaim(c.ClaimText) == job.ClaimHash);
        if (claim is null)
        {
            // O conteúdo mudou desde o enqueue — claim não existe mais.
            await queue.MarkFailedAsync(job.Id, "Claim no longer extractable from current content", MaxAttempts, ct);
            return true;
        }

        // PR 34g — reflexion: se esse job já tentou antes (AttemptCount > 1
        // pós-incremento no ClaimNext) e tem LastError, passa o feedback
        // pro generator injetar guidance de autocorreção no prompt.
        // AttemptCount já foi incrementado no ClaimNextAsync; >1 significa retry.
        RetryFeedback? priorFailure = null;
        if (job.AttemptCount > 1 && !string.IsNullOrWhiteSpace(job.LastError))
        {
            var (reason, detail) = ParseLastError(job.LastError);
            priorFailure = new RetryFeedback(reason, detail, job.AttemptCount - 1);
        }

        // Gera!
        GroundedGenerationResult result;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            result = await generator.GenerateAsync(claim, content.Title, priorFailure, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            await queue.MarkFailedAsync(job.Id, $"Generator threw: {ex.Message}", MaxAttempts, ct);
            return true;
        }
        sw.Stop();

        if (!result.IsSuccess)
        {
            await queue.MarkFailedAsync(job.Id,
                $"{result.FailureReason}: {result.FailureDetail}", MaxAttempts, ct);
            return true;
        }

        // Persiste GeneratedChallenge.
        // TopicId = ContentId (decisão pragmática 1:1 — Topic é projetado
        // do KnowledgeGraph e tem 1 por Content por enquanto; refinar
        // quando KnowledgeGraph virar n-pra-1).
        var bodyJson = JsonSerializer.Serialize(new
        {
            options      = result.Question!.Options,
            correctIndex = result.Question.CorrectIndex,
            explanation  = result.Question.Explanation,
            // PR 34a — shape persistido pra UI renderizar (InlineBlank
            // quando FillInTheBlank, cards padrão quando MultipleChoice).
            // Rows pré-PR-34a não têm o campo; parsers tratam ausência
            // como "MultipleChoice".
            shape        = result.Question.Shape.ToString(),
            // Metadados úteis pra debug:
            sourceChunkIndex = result.Question.SourceChunkIndex,
            generatedBy      = "llm-grounded",
        });
        var challenge = new GeneratedChallenge
        {
            ContentId           = job.ContentId,
            TopicId             = job.ContentId,
            TrailId             = content.TrailId,
            Strategy            = ForgeStrategy.LlmGrounded,
            Prompt              = result.Question.Prompt,
            BodyJson            = bodyJson,
            EstimatedDifficulty = 0.5, // Calibrar via PR 33
            IsActive            = true,
        };
        db.GeneratedChallenge.Add(challenge);
        await db.SaveChangesAsync(ct);

        await queue.MarkDoneAsync(job.Id, challenge.Id, ct);
        log.LogInformation(
            "Job {Id} → challenge {ChallengeId} [shape={Shape}] for content {ContentId} in {Elapsed}ms{Retry}",
            job.Id, challenge.Id, result.Question.Shape, job.ContentId, sw.ElapsedMilliseconds,
            priorFailure is not null ? $" (recovered on attempt {job.AttemptCount})" : "");
        return true;
    }

    /// <summary>
    /// PR 34g — parseia o <c>LastError</c> persistido (formato
    /// "{Reason}: {Detail}") de volta em <see cref="GenerationFailureReason"/>
    /// + detalhe, pra alimentar a reflexion. Tolerante: se não casar o
    /// enum, usa <c>SchemaInvalid</c> como bucket genérico (guidance
    /// ainda é útil).
    /// </summary>
    private static (GenerationFailureReason, string?) ParseLastError(string lastError)
    {
        var idx = lastError.IndexOf(':');
        var head = idx > 0 ? lastError[..idx].Trim() : lastError.Trim();
        var detail = idx > 0 && idx + 1 < lastError.Length ? lastError[(idx + 1)..].Trim() : null;
        return Enum.TryParse<GenerationFailureReason>(head, out var reason)
            ? (reason, detail)
            : (GenerationFailureReason.SchemaInvalid, lastError);
    }
}
