using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge.Queue;

/// <summary>
/// Implementação Postgres da <see cref="IQuestionForgeQueue"/>. Usa
/// transação serializável + SKIP LOCKED no <c>ClaimNext</c> pra
/// garantir que jobs não sejam processados duas vezes mesmo com
/// múltiplos workers (futuro — hoje rodamos 1 worker singleton).
///
/// <para>Por enquanto, com 1 worker, a abordagem simples (transação
/// padrão + FirstOrDefault) é suficiente. Migração pra FOR UPDATE SKIP
/// LOCKED vira PR quando escalarmos pra N workers paralelos.</para>
/// </summary>
public sealed class QuestionForgeQueueService(
    ApplicationDbContext db,
    ILogger<QuestionForgeQueueService> log) : IQuestionForgeQueue
{
    public async Task<int> EnqueueForContentAsync(
        int contentId,
        IReadOnlyList<ClaimCandidate> claims,
        ForgeJobPriority priority = ForgeJobPriority.Normal,
        CancellationToken ct = default)
    {
        if (claims.Count == 0) return 0;

        // Pre-busca dedup: hashes que já existem pendentes/running pro Content.
        var hashes = claims.Select(c => HashClaim(c.ClaimText)).ToList();
        var existingHashes = await db.QuestionForgeJob
            .Where(j => j.ContentId == contentId
                     && hashes.Contains(j.ClaimHash)
                     && (j.Status == ForgeJobStatus.Pending || j.Status == ForgeJobStatus.Running))
            .Select(j => j.ClaimHash)
            .ToListAsync(ct);
        var existing = new HashSet<string>(existingHashes);

        var added = 0;
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            var hash  = hashes[i];
            if (existing.Contains(hash)) continue;

            db.QuestionForgeJob.Add(new QuestionForgeJob
            {
                ContentId  = contentId,
                ChunkIndex = claim.ChunkIndex,
                ClaimText  = claim.ClaimText,
                ClaimHash  = hash,
                Status     = ForgeJobStatus.Pending,
                Priority   = priority,
                EnqueuedAt = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Enqueued {Count} forge jobs for content {ContentId} ({Priority})",
                added, contentId, priority);
        }
        return added;
    }

    public async Task<QuestionForgeJob?> ClaimNextAsync(CancellationToken ct = default)
    {
        // Urgent primeiro, depois FIFO. Tx pra evitar race com outros
        // workers (hoje 1 só, mas defensivo). InMemoryDatabase (testes
        // unitários) não suporta transações reais — skip.
        var supportsTransaction = db.Database.ProviderName is not null
            && !db.Database.ProviderName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

        await using var tx = supportsTransaction
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var job = await db.QuestionForgeJob
            .Where(j => j.Status == ForgeJobStatus.Pending)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.Id)
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            if (tx is not null) await tx.CommitAsync(ct);
            return null;
        }

        job.Status       = ForgeJobStatus.Running;
        job.StartedAt    = DateTime.UtcNow;
        job.AttemptCount++;
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);

        return job;
    }

    public async Task MarkDoneAsync(long jobId, int generatedChallengeId, CancellationToken ct = default)
    {
        var job = await db.QuestionForgeJob.FindAsync(new object[] { jobId }, ct);
        if (job is null) return;

        job.Status               = ForgeJobStatus.Done;
        job.CompletedAt          = DateTime.UtcNow;
        job.GeneratedChallengeId = generatedChallengeId;
        job.LastError            = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(long jobId, string error, int maxAttempts, CancellationToken ct = default)
    {
        var job = await db.QuestionForgeJob.FindAsync(new object[] { jobId }, ct);
        if (job is null) return;

        job.LastError = error.Length > 2000 ? error[..2000] : error;

        if (job.AttemptCount >= maxAttempts)
        {
            job.Status      = ForgeJobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            log.LogWarning("Job {Id} failed permanently after {Attempts} attempts: {Error}",
                jobId, job.AttemptCount, error);
        }
        else
        {
            // Reseta pra Pending pra retry — worker pega de novo
            // (com algum backoff conforme política do consumer)
            job.Status    = ForgeJobStatus.Pending;
            job.StartedAt = null;
            log.LogDebug("Job {Id} attempt {N}/{Max} failed, requeueing: {Error}",
                jobId, job.AttemptCount, maxAttempts, error);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<ForgeQueueStatus> GetStatusAsync(CancellationToken ct = default)
    {
        // Single roundtrip com group by — barato mesmo com fila grande.
        var counts = await db.QuestionForgeJob
            .GroupBy(j => new { j.Status, j.Priority })
            .Select(g => new { g.Key.Status, g.Key.Priority, Count = g.Count() })
            .ToListAsync(ct);

        int Of(ForgeJobStatus s, ForgeJobPriority? p = null) =>
            counts.Where(c => c.Status == s && (p == null || c.Priority == p)).Sum(c => c.Count);

        return new ForgeQueueStatus(
            Pending:        Of(ForgeJobStatus.Pending),
            Running:        Of(ForgeJobStatus.Running),
            Done:           Of(ForgeJobStatus.Done),
            Failed:         Of(ForgeJobStatus.Failed),
            UrgentPending:  Of(ForgeJobStatus.Pending, ForgeJobPriority.Urgent));
    }

    /// <summary>Hash determinístico do claim — SHA-256 truncado em 16
    /// hex chars (64 bits) é suficiente pra dedup (1 colisão em 4 bilhões).</summary>
    internal static string HashClaim(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }
}
