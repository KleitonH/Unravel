using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Fila persistida de jobs de geração de perguntas LLM-grounded (PR 32).
/// Operações:
/// <list type="bullet">
///   <item><b>Enqueue</b> — pra cada (ContentId, claim) adiciona um job
///   pendente, dedupando por <c>(ContentId, ChunkIndex, ClaimHash)</c>.</item>
///   <item><b>ClaimNext</b> — o worker pega o próximo job pendente
///   (urgent primeiro, depois FIFO).</item>
///   <item><b>MarkDone/MarkFailed</b> — fecha o job com resultado.</item>
///   <item><b>Status</b> — telemetria/UI admin: contagens por status.</item>
/// </list>
/// </summary>
public interface IQuestionForgeQueue
{
    /// <summary>
    /// Enfileira N jobs pra um Content (1 por claim). Retorna número
    /// efetivamente adicionado (dedup pode reduzir).
    ///
    /// <para>PR 52a — <paramref name="batchId"/> agrupa jobs disparados
    /// na mesma chamada do moderador (pra UI de progresso saber quais
    /// jobs são "dele"). <paramref name="enqueuedByUserId"/> permite
    /// filtrar batches no admin panel sem expor batches de outros admins.
    /// Ambos opcionais — null pra cron noturno / sistema.</para>
    /// </summary>
    Task<int> EnqueueForContentAsync(
        int contentId,
        IReadOnlyList<ClaimCandidate> claims,
        ForgeJobPriority priority = ForgeJobPriority.Normal,
        Guid? batchId = null,
        Guid? enqueuedByUserId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Pega o próximo job pendente atômicamente (transação) e marca
    /// como Running. Retorna null se fila vazia.
    /// </summary>
    Task<QuestionForgeJob?> ClaimNextAsync(CancellationToken ct = default);

    /// <summary>
    /// Fecha o job como sucesso, anexa GeneratedChallenge.Id.
    /// </summary>
    Task MarkDoneAsync(long jobId, int generatedChallengeId, CancellationToken ct = default);

    /// <summary>
    /// Fecha o job como falha. Se attemptCount < maxAttempts, reseta
    /// pra Pending pra retry; caso contrário marca Failed definitivo.
    /// <para>Retorna <c>true</c> se a falha foi <b>definitiva</b> (job virou
    /// <c>Failed</c> nesta chamada) — o worker usa isso pra disparar
    /// reposição/estorno (PR 62). <c>false</c> em retry.</para>
    /// </summary>
    Task<bool> MarkFailedAsync(long jobId, string error, int maxAttempts, CancellationToken ct = default);

    /// <summary>
    /// Snapshot da fila pra dashboard admin.
    /// </summary>
    Task<ForgeQueueStatus> GetStatusAsync(CancellationToken ct = default);
}

public sealed record ForgeQueueStatus(
    int Pending,
    int Running,
    int Done,
    int Failed,
    int UrgentPending);
