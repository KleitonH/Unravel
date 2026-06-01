using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Forge.Queue;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Forge.Queue;

/// <summary>
/// Cobre operações da fila contra um DbContext InMemory: enqueue
/// (dedup + priority), claim (ordem urgent → fifo), mark done/failed
/// (retry policy), status snapshot. Filtered-unique-index do Postgres
/// não é verificado aqui mas a lógica de dedup por código é.
/// </summary>
public class QuestionForgeQueueServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly QuestionForgeQueueService _sut;

    public QuestionForgeQueueServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new ApplicationDbContext(options);
        _sut = new QuestionForgeQueueService(_db, NullLogger<QuestionForgeQueueService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static ClaimCandidate Claim(int chunk, string text) =>
        new(chunk, $"chunk text {chunk}", text, 0.8);

    // ── Enqueue ─────────────────────────────────────────────────────

    [Fact]
    public async Task Enqueue_FreshDb_AddsAllJobs()
    {
        var claims = new[]
        {
            Claim(0, "claim A"),
            Claim(1, "claim B"),
            Claim(2, "claim C"),
        };
        var n = await _sut.EnqueueForContentAsync(42, claims);

        Assert.Equal(3, n);
        var jobs = await _db.QuestionForgeJob.ToListAsync();
        Assert.All(jobs, j => Assert.Equal(42, j.ContentId));
        Assert.All(jobs, j => Assert.Equal(ForgeJobStatus.Pending, j.Status));
    }

    [Fact]
    public async Task Enqueue_DedupesByContentChunkAndHash()
    {
        var claims = new[] { Claim(0, "mesmo claim") };
        await _sut.EnqueueForContentAsync(42, claims);

        var added = await _sut.EnqueueForContentAsync(42, claims);
        Assert.Equal(0, added);
        Assert.Single(await _db.QuestionForgeJob.ToListAsync());
    }

    [Fact]
    public async Task Enqueue_DifferentContent_NoDedup()
    {
        var claims = new[] { Claim(0, "claim igual") };
        await _sut.EnqueueForContentAsync(42, claims);
        await _sut.EnqueueForContentAsync(43, claims);

        Assert.Equal(2, await _db.QuestionForgeJob.CountAsync());
    }

    [Fact]
    public async Task Enqueue_SameClaimAfterDone_NoDedup()
    {
        var claims = new[] { Claim(0, "claim") };
        await _sut.EnqueueForContentAsync(42, claims);

        var job = await _db.QuestionForgeJob.FirstAsync();
        await _sut.MarkDoneAsync(job.Id, 999);

        // Re-enqueue mesmo claim — dedup só vê Pending/Running
        var added = await _sut.EnqueueForContentAsync(42, claims);
        Assert.Equal(1, added);
    }

    [Fact]
    public async Task Enqueue_Empty_ReturnsZero()
    {
        Assert.Equal(0, await _sut.EnqueueForContentAsync(42, Array.Empty<ClaimCandidate>()));
    }

    // ── ClaimNext ───────────────────────────────────────────────────

    [Fact]
    public async Task ClaimNext_EmptyQueue_ReturnsNull()
    {
        Assert.Null(await _sut.ClaimNextAsync());
    }

    [Fact]
    public async Task ClaimNext_PicksFifo_WhenSamePriority()
    {
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "first") });
        await _sut.EnqueueForContentAsync(1, new[] { Claim(1, "second") });
        await _sut.EnqueueForContentAsync(1, new[] { Claim(2, "third") });

        var j1 = await _sut.ClaimNextAsync();
        var j2 = await _sut.ClaimNextAsync();
        var j3 = await _sut.ClaimNextAsync();

        Assert.Equal("first",  j1!.ClaimText);
        Assert.Equal("second", j2!.ClaimText);
        Assert.Equal("third",  j3!.ClaimText);
    }

    [Fact]
    public async Task ClaimNext_UrgentBeatsNormal_EvenIfEnqueuedLater()
    {
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "normal first") }, ForgeJobPriority.Normal);
        await _sut.EnqueueForContentAsync(2, new[] { Claim(1, "urgent later") }, ForgeJobPriority.Urgent);

        var j = await _sut.ClaimNextAsync();
        Assert.Equal("urgent later", j!.ClaimText);
        Assert.Equal(ForgeJobPriority.Urgent, j.Priority);
    }

    [Fact]
    public async Task ClaimNext_TransitionsToRunningAndIncrementsAttempt()
    {
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "x") });
        var j = await _sut.ClaimNextAsync();

        Assert.Equal(ForgeJobStatus.Running, j!.Status);
        Assert.NotNull(j.StartedAt);
        Assert.Equal(1, j.AttemptCount);

        // Outro Claim agora não pega o mesmo
        Assert.Null(await _sut.ClaimNextAsync());
    }

    // ── MarkDone / MarkFailed ──────────────────────────────────────

    [Fact]
    public async Task MarkDone_SetsStatusAndChallengeId()
    {
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "x") });
        var j = await _sut.ClaimNextAsync();

        await _sut.MarkDoneAsync(j!.Id, generatedChallengeId: 5050);

        var stored = await _db.QuestionForgeJob.FindAsync(j.Id);
        Assert.Equal(ForgeJobStatus.Done, stored!.Status);
        Assert.Equal(5050, stored.GeneratedChallengeId);
        Assert.NotNull(stored.CompletedAt);
    }

    [Fact]
    public async Task MarkFailed_BelowMaxAttempts_RequeuesAsPending()
    {
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "x") });
        var j = await _sut.ClaimNextAsync();

        await _sut.MarkFailedAsync(j!.Id, "ollama timeout", maxAttempts: 3);

        var stored = await _db.QuestionForgeJob.FindAsync(j.Id);
        Assert.Equal(ForgeJobStatus.Pending, stored!.Status);
        Assert.Equal("ollama timeout", stored.LastError);
        Assert.Null(stored.StartedAt); // reset pra ClaimNext pegar de novo
        Assert.Equal(1, stored.AttemptCount); // mantém o count
    }

    [Fact]
    public async Task MarkFailed_AtMaxAttempts_MarksFailed()
    {
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "x") });
        var j = await _sut.ClaimNextAsync(); // attempt 1
        await _sut.MarkFailedAsync(j!.Id, "fail", 3);
        j = await _sut.ClaimNextAsync();      // attempt 2
        await _sut.MarkFailedAsync(j!.Id, "fail", 3);
        j = await _sut.ClaimNextAsync();      // attempt 3
        await _sut.MarkFailedAsync(j!.Id, "fail", 3);

        var stored = await _db.QuestionForgeJob.FindAsync(j!.Id);
        Assert.Equal(ForgeJobStatus.Failed, stored!.Status);
        Assert.NotNull(stored.CompletedAt);
    }

    [Fact]
    public async Task MarkFailed_TruncatesLongError()
    {
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "x") });
        var j = await _sut.ClaimNextAsync();

        var hugeError = new string('e', 3000);
        await _sut.MarkFailedAsync(j!.Id, hugeError, 3);

        var stored = await _db.QuestionForgeJob.FindAsync(j.Id);
        Assert.NotNull(stored!.LastError);
        Assert.Equal(2000, stored.LastError!.Length);
    }

    // ── GetStatus ───────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_GroupsCorrectly()
    {
        // 2 normal pending, 1 urgent pending, 1 done
        await _sut.EnqueueForContentAsync(1, new[] { Claim(0, "n1"), Claim(1, "n2") });
        await _sut.EnqueueForContentAsync(2, new[] { Claim(0, "u1") }, ForgeJobPriority.Urgent);

        var doneJob = await _sut.ClaimNextAsync(); // urgent
        await _sut.MarkDoneAsync(doneJob!.Id, 1);

        var s = await _sut.GetStatusAsync();
        Assert.Equal(2, s.Pending);
        Assert.Equal(0, s.Running);
        Assert.Equal(1, s.Done);
        Assert.Equal(0, s.Failed);
        Assert.Equal(0, s.UrgentPending); // já foi consumida
    }

    // ── HashClaim ──────────────────────────────────────────────────

    [Fact]
    public void HashClaim_DeterministicAndSensitive()
    {
        var a = QuestionForgeQueueService.HashClaim("foo bar");
        var b = QuestionForgeQueueService.HashClaim("foo bar");
        var c = QuestionForgeQueueService.HashClaim("foo baz");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(16, a.Length); // 8 bytes hex = 16 chars
    }

    [Fact]
    public void HashClaim_TrimsWhitespace()
    {
        Assert.Equal(
            QuestionForgeQueueService.HashClaim("foo"),
            QuestionForgeQueueService.HashClaim("  foo  "));
    }
}
