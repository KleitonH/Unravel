using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Forge.Eval;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Forge.Eval;

/// <summary>
/// Cobre o <see cref="ModeratorGoldReader"/>: filtragem por trilha,
/// skip silencioso de items malformados (distratores != 3, JSON
/// inválido, campos vazios), conversão pro schema GoldItem.
/// </summary>
public class ModeratorGoldReaderTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public ModeratorGoldReaderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        // Seed Trail + Content (pra resolução de slug via FK)
        var trail = new Trail { Id = 1, Slug = "angular-fundamentos", Name = "Angular" };
        _db.Trail.Add(trail);
        _db.Content.AddRange(
            new Content { Id = 10, TrailId = 1, Slug = "angular-componentes", Title = "Componentes", Body = "X" },
            new Content { Id = 11, TrailId = 1, Slug = "angular-templates",  Title = "Templates",  Body = "X" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void AddGold(string contentSlug, List<string>? distractors = null,
        string prompt = "P?", string correctAnswer = "C", bool active = true,
        string? explanation = "E")
    {
        var content = _db.Content.First(c => c.Slug == contentSlug);
        _db.ModeratorGoldItem.Add(new ModeratorGoldItem
        {
            ContentId       = content.Id,
            CuratorUserId   = Guid.NewGuid(),
            Prompt          = prompt,
            CorrectAnswer   = correctAnswer,
            DistractorsJson = System.Text.Json.JsonSerializer.Serialize(distractors ?? new() { "D1", "D2", "D3" }),
            Explanation     = explanation,
            IsActive        = active,
            CreatedAt       = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Read_EmptyDb_ReturnsEmpty()
    {
        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.Empty(items);
    }

    [Fact]
    public async Task Read_ValidItems_AreReturned()
    {
        AddGold("angular-componentes");
        AddGold("angular-templates");

        // Diagnóstico: separa qual asserção falha
        var trailCount   = _db.Trail.Count();
        var contentCount = _db.Content.Count();
        var goldCount    = _db.ModeratorGoldItem.Count();
        Assert.True(trailCount   == 1, $"Trail count = {trailCount}");
        Assert.True(contentCount == 2, $"Content count = {contentCount}");
        Assert.True(goldCount    == 2, $"Gold count = {goldCount}");

        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.True(items.Count == 2, $"Reader returned {items.Count} items (expected 2)");
    }

    [Fact]
    public async Task Read_DifferentTrail_NotReturned()
    {
        // seed outro trail+content
        var otherTrail = new Trail { Id = 2, Slug = "python-fundamentos", Name = "Python" };
        _db.Trail.Add(otherTrail);
        _db.Content.Add(new Content { Id = 20, TrailId = 2, Slug = "python-basico", Title = "X", Body = "X" });
        _db.SaveChanges();

        var py = _db.Content.First(c => c.Slug == "python-basico");
        _db.ModeratorGoldItem.Add(new ModeratorGoldItem
        {
            ContentId = py.Id, CuratorUserId = Guid.NewGuid(),
            Prompt = "P?", CorrectAnswer = "C",
            DistractorsJson = "[\"a\",\"b\",\"c\"]", Explanation = "E", IsActive = true,
        });
        _db.SaveChanges();
        AddGold("angular-componentes");

        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.Single(items);
        Assert.Equal("angular-componentes", items[0].TopicSlug);
    }

    [Fact]
    public async Task Read_InactiveItem_SkippedSilently()
    {
        AddGold("angular-componentes", active: true);
        AddGold("angular-templates", active: false);

        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.Single(items);
        Assert.Equal("angular-componentes", items[0].TopicSlug);
    }

    [Fact]
    public async Task Read_WrongNumberOfDistractors_SkippedSilently()
    {
        AddGold("angular-componentes", distractors: new() { "D1", "D2" }); // só 2
        AddGold("angular-templates",   distractors: new() { "D1", "D2", "D3" }); // 3 OK

        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.Single(items);
        Assert.Equal("angular-templates", items[0].TopicSlug);
    }

    [Fact]
    public async Task Read_BlankDistractor_SkippedSilently()
    {
        AddGold("angular-componentes", distractors: new() { "D1", "", "D3" });
        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.Empty(items);
    }

    [Fact]
    public async Task Read_MalformedJson_SkippedSilently()
    {
        var content = _db.Content.First(c => c.Slug == "angular-componentes");
        _db.ModeratorGoldItem.Add(new ModeratorGoldItem
        {
            ContentId = content.Id, CuratorUserId = Guid.NewGuid(),
            Prompt = "P?", CorrectAnswer = "C",
            DistractorsJson = "{not json}", Explanation = "E", IsActive = true,
        });
        await _db.SaveChangesAsync();

        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.Empty(items);
    }

    [Fact]
    public async Task Read_NullDifficultyHint_DefaultsTo050()
    {
        var content = _db.Content.First(c => c.Slug == "angular-componentes");
        _db.ModeratorGoldItem.Add(new ModeratorGoldItem
        {
            ContentId = content.Id, CuratorUserId = Guid.NewGuid(),
            Prompt = "P?", CorrectAnswer = "C",
            DistractorsJson = "[\"a\",\"b\",\"c\"]", Explanation = "E",
            DifficultyHint = null, IsActive = true,
        });
        await _db.SaveChangesAsync();

        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "angular-fundamentos");
        Assert.Single(items);
        Assert.Equal(0.50, items[0].DifficultyHint);
    }

    [Fact]
    public async Task Read_EmptyTrailSlug_ReturnsEmpty()
    {
        var items = await ModeratorGoldReader.ReadForTrailAsync(_db, "");
        Assert.Empty(items);
    }
}
