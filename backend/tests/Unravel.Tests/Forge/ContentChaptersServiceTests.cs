using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Forge;

/// <summary>
/// PR 60-a — Cobre ContentChaptersService: agrupamento por chunkIndex,
/// quota adaptativa (4..7 baseado em difficulty), readiness gate.
/// Usa InMemory DB; ChunkSegmenter é puro CPU (sem mock).
/// </summary>
public class ContentChaptersServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ContentChaptersService _sut;

    public ContentChaptersServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new ApplicationDbContext(options);
        _sut = new ContentChaptersService(_db);
    }

    public void Dispose() => _db.Dispose();

    private const string ThreeChapterBody = @"
## Capítulo Um

Texto introdutório do primeiro tópico do conteúdo. Algumas frases
descrevendo o conceito A pra alunos iniciantes.

## Capítulo Dois

Texto do segundo tópico, ainda introdutório mas mais técnico, com
referência a `apiCall` e padrões de uso comuns.

## Capítulo Três

Último tópico, conclusivo. Junta os conceitos anteriores e dá exemplos
práticos pra fixação.
";

    private async Task SeedContent(int contentId, string body = ThreeChapterBody)
    {
        _db.Content.Add(new Content
        {
            Id = contentId, Title = "Test", Body = body, IsActive = true,
            TrailId = 1, ChallengesRequired = 5,
        });
        await _db.SaveChangesAsync();
    }

    private async Task SeedChallenges(int contentId, int chunkIndex, int count, double difficulty = 0.5)
    {
        for (var i = 0; i < count; i++)
        {
            var body = JsonSerializer.Serialize(new
            {
                options          = new[] { "A", "B", "C", "D" },
                correctIndex     = 0,
                explanation      = "ok",
                shape            = "MultipleChoice",
                sourceChunkIndex = chunkIndex,
            });
            _db.GeneratedChallenge.Add(new GeneratedChallenge
            {
                ContentId           = contentId,
                TopicId             = contentId,
                TrailId             = 1,
                Strategy            = ForgeStrategy.LlmGrounded,
                Prompt              = $"q chunk{chunkIndex} #{i}",
                BodyJson            = body,
                EstimatedDifficulty = difficulty,
                IsActive            = true,
            });
        }
        await _db.SaveChangesAsync();
    }

    // ── GetChaptersAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetChapters_ContentNotExists_ReturnsNull()
    {
        var r = await _sut.GetChaptersAsync(999, 4, 7);
        Assert.Null(r);
    }

    [Fact]
    public async Task GetChapters_NoChallengesYet_ReturnsChaptersWithEmptyPools()
    {
        await SeedContent(1);
        var r = await _sut.GetChaptersAsync(1, 4, 7);
        Assert.NotNull(r);
        Assert.Equal(3, r!.Chapters.Count);
        Assert.All(r.Chapters, c => Assert.Empty(c.Challenges));
        Assert.Equal("Capítulo Um", r.Chapters[0].Title);
    }

    [Fact]
    public async Task GetChapters_GroupsByChunkIndex()
    {
        await SeedContent(1);
        // difficulty 0.1 → extra = round(0.1 * 3) = 0 → quota = min = 4
        await SeedChallenges(1, chunkIndex: 0, count: 5, difficulty: 0.1);
        await SeedChallenges(1, chunkIndex: 1, count: 4, difficulty: 0.1);
        await SeedChallenges(1, chunkIndex: 2, count: 3, difficulty: 0.1);

        var r = await _sut.GetChaptersAsync(1, 4, 7);
        Assert.NotNull(r);
        // Chunk 0: low difficulty → quota 4; pool 5 → returns 4
        Assert.Equal(4, r!.Chapters[0].Challenges.Count);
        // Chunk 1: pool == min → returns all 4
        Assert.Equal(4, r.Chapters[1].Challenges.Count);
        // Chunk 2: pool < min → returns 3 (gap; UI sinaliza)
        Assert.Equal(3, r.Chapters[2].Challenges.Count);
    }

    [Fact]
    public async Task GetChapters_HighDifficulty_AllocatesMore()
    {
        await SeedContent(1);
        await SeedChallenges(1, chunkIndex: 0, count: 10, difficulty: 1.0); // máximo

        var r = await _sut.GetChaptersAsync(1, 4, 7);
        // difficulty avg 1.0 → extra = round(1.0 * (7-4)) = 3 → quota = 7
        Assert.Equal(7, r!.Chapters[0].Challenges.Count);
    }

    [Fact]
    public async Task GetChapters_LowDifficulty_AllocatesMin()
    {
        await SeedContent(1);
        await SeedChallenges(1, chunkIndex: 0, count: 10, difficulty: 0.0); // fácil

        var r = await _sut.GetChaptersAsync(1, 4, 7);
        // difficulty avg 0 → extra = 0 → quota = 4
        Assert.Equal(4, r!.Chapters[0].Challenges.Count);
    }

    [Fact]
    public async Task GetChapters_OrdersChallengesByDifficultyAsc()
    {
        await SeedContent(1);
        // Mistura difficulties
        var ds = new[] { 0.9, 0.1, 0.5, 0.3, 0.7 };
        foreach (var d in ds)
            await SeedChallenges(1, chunkIndex: 0, count: 1, difficulty: d);

        var r = await _sut.GetChaptersAsync(1, 4, 7);
        var diffs = r!.Chapters[0].Challenges.Select(c => c.EstimatedDifficulty).ToList();
        Assert.Equal(diffs.OrderBy(d => d), diffs);
    }

    // ── AdaptiveQuota (interna) ─────────────────────────────────────

    [Theory]
    [InlineData(0, 4, 7, 0)]    // pool vazio
    [InlineData(2, 4, 7, 2)]    // pool < min
    [InlineData(4, 4, 7, 4)]    // pool == min
    [InlineData(20, 4, 4, 4)]   // min == max
    public void AdaptiveQuota_EdgeCases(int poolCount, int min, int max, int expected)
    {
        var pool = Enumerable.Range(0, poolCount)
            .Select(_ => new GeneratedChallenge { EstimatedDifficulty = 0.5 })
            .ToList();
        Assert.Equal(expected, ContentChaptersService.AdaptiveQuota(pool, min, max));
    }

    // ── GetReadinessAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetReadiness_AllChaptersAbove_ReturnsReady()
    {
        await SeedContent(1);
        await SeedChallenges(1, 0, 4);
        await SeedChallenges(1, 1, 5);
        await SeedChallenges(1, 2, 4);

        var r = await _sut.GetReadinessAsync(1, requiredPerChapter: 4);
        Assert.True(r!.Ready);
        Assert.All(r.Chapters, c => Assert.True(c.Ready));
    }

    [Fact]
    public async Task GetReadiness_OneChapterShort_ReturnsNotReady()
    {
        await SeedContent(1);
        await SeedChallenges(1, 0, 4);
        await SeedChallenges(1, 1, 3); // ← falta 1
        await SeedChallenges(1, 2, 4);

        var r = await _sut.GetReadinessAsync(1, requiredPerChapter: 4);
        Assert.False(r!.Ready);
        Assert.True(r.Chapters[0].Ready);
        Assert.False(r.Chapters[1].Ready);
        Assert.Equal(3, r.Chapters[1].Current);
        Assert.True(r.Chapters[2].Ready);
    }

    [Fact]
    public async Task GetReadiness_EmptyContent_NotReady()
    {
        _db.Content.Add(new Content { Id = 1, Title = "Empty", Body = "", IsActive = true, TrailId = 1 });
        await _db.SaveChangesAsync();
        var r = await _sut.GetReadinessAsync(1, requiredPerChapter: 4);
        Assert.False(r!.Ready);
        Assert.Empty(r.Chapters);
    }
}
