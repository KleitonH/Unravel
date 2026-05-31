using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Knowledge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Knowledge;

/// <summary>
/// Cobre o pipeline do <see cref="KnowledgeImporter"/>: parser de
/// frontmatter, upsert idempotente por slug, validação de campos
/// obrigatórios.
///
/// <para>Usa <c>UseInMemoryDatabase</c> — o filtered-unique-index do
/// Postgres não é validado aqui (InMemory ignora), mas a lógica de
/// busca por slug + atualização é igual.</para>
/// </summary>
public class KnowledgeImporterTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ApplicationDbContext _db;
    private readonly KnowledgeImporter _sut;

    public KnowledgeImporterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"unravel-knowledge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new ApplicationDbContext(options);
        _sut = new KnowledgeImporter(_db, NullLogger<KnowledgeImporter>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }

    // ── SplitFrontmatter ────────────────────────────────────────────

    [Fact]
    public void SplitFrontmatter_ValidMarkdown_ReturnsParts()
    {
        var raw = "---\nslug: foo\ntitle: Foo\n---\n# Conteúdo\n\nCorpo aqui.";
        var (fm, body) = KnowledgeImporter.SplitFrontmatter(raw, "test.md");

        Assert.Contains("slug: foo", fm);
        Assert.Contains("Corpo aqui.", body);
        Assert.DoesNotContain("---", fm);
    }

    [Fact]
    public void SplitFrontmatter_HandlesCrlfLineEndings()
    {
        var raw = "---\r\nslug: x\r\n---\r\nbody";
        var (fm, body) = KnowledgeImporter.SplitFrontmatter(raw, "test.md");
        Assert.Contains("slug: x", fm);
        Assert.Equal("body", body);
    }

    [Fact]
    public void SplitFrontmatter_NoFrontmatter_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            KnowledgeImporter.SplitFrontmatter("# Sem frontmatter\n\nCorpo.", "test.md"));
    }

    [Fact]
    public void SplitFrontmatter_UnterminatedFrontmatter_Throws()
    {
        var raw = "---\nslug: x\n# corpo sem fechar frontmatter";
        Assert.Throws<InvalidOperationException>(() =>
            KnowledgeImporter.SplitFrontmatter(raw, "test.md"));
    }

    // ── ImportAllAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ImportAll_FreshDb_CreatesTrailAndContents()
    {
        WriteTrail("test-trail", name: "Test Trail", contents: [
            ("01-intro",    "Intro",    "Body 1"),
            ("02-advanced", "Advanced", "Body 2"),
        ]);

        var summary = await _sut.ImportAllAsync(_tempRoot);

        Assert.Equal(1, summary.TrailsCreated);
        Assert.Equal(0, summary.TrailsUpdated);
        Assert.Equal(2, summary.ContentsCreated);
        Assert.Equal(0, summary.ContentsUpdated);

        var trail = await _db.Trail.SingleAsync();
        Assert.Equal("test-trail", trail.Slug);
        Assert.Equal("Test Trail", trail.Name);

        var contents = await _db.Content.Where(c => c.TrailId == trail.Id).ToListAsync();
        Assert.Equal(2, contents.Count);
        Assert.Contains(contents, c => c.Slug == "01-intro" && c.Title == "Intro" && c.Body == "Body 1");
    }

    [Fact]
    public async Task ImportAll_TwiceSameSource_IsIdempotent()
    {
        WriteTrail("idem", name: "Idem", contents: [("a", "A", "Body A")]);

        await _sut.ImportAllAsync(_tempRoot);
        var s2 = await _sut.ImportAllAsync(_tempRoot);

        Assert.Equal(0, s2.TrailsCreated);
        Assert.Equal(1, s2.TrailsUpdated);
        Assert.Equal(0, s2.ContentsCreated);
        Assert.Equal(1, s2.ContentsUpdated);

        Assert.Single(await _db.Trail.ToListAsync());
        Assert.Single(await _db.Content.ToListAsync());
    }

    [Fact]
    public async Task ImportAll_UpdatedBody_OverwritesContent()
    {
        WriteTrail("updt", name: "U", contents: [("x", "X", "Original body")]);
        await _sut.ImportAllAsync(_tempRoot);

        // Reescreve o mesmo MD com body diferente.
        WriteTrail("updt", name: "U", contents: [("x", "X v2", "Brand new body")]);
        await _sut.ImportAllAsync(_tempRoot);

        var c = await _db.Content.SingleAsync(c => c.Slug == "x");
        Assert.Equal("X v2", c.Title);
        Assert.Equal("Brand new body", c.Body);
    }

    [Fact]
    public async Task ImportAll_MissingSlugInFrontmatter_Throws()
    {
        var trailDir = Path.Combine(_tempRoot, "bad");
        Directory.CreateDirectory(trailDir);
        File.WriteAllText(Path.Combine(trailDir, "_trail.yaml"), "slug: bad\nname: Bad\n");
        File.WriteAllText(Path.Combine(trailDir, "01-no-slug.md"),
            "---\ntitle: Sem Slug\norder: 1\n---\nCorpo.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ImportAllAsync(_tempRoot));
    }

    [Fact]
    public async Task ImportAll_DirWithoutManifest_IsSkipped()
    {
        // Trilha válida
        WriteTrail("ok", name: "OK", contents: [("x", "X", "body")]);
        // Diretório sem _trail.yaml — deve ser ignorado, não erro
        Directory.CreateDirectory(Path.Combine(_tempRoot, "no-manifest"));
        File.WriteAllText(Path.Combine(_tempRoot, "no-manifest", "01-orphan.md"),
            "---\nslug: orphan\ntitle: Orphan\n---\nbody");

        var s = await _sut.ImportAllAsync(_tempRoot);

        Assert.Equal(1, s.TrailsCreated);
        Assert.Equal(1, s.ContentsCreated);
        Assert.DoesNotContain(await _db.Content.ToListAsync(), c => c.Slug == "orphan");
    }

    [Fact]
    public async Task ImportAll_NonExistentRoot_ReturnsEmptySummaryNoThrow()
    {
        var summary = await _sut.ImportAllAsync(Path.Combine(_tempRoot, "nope"));
        Assert.Equal(default, summary);
    }

    [Fact]
    public async Task ImportAll_LevelParsesCaseInsensitively()
    {
        var trailDir = Path.Combine(_tempRoot, "lvl");
        Directory.CreateDirectory(trailDir);
        File.WriteAllText(Path.Combine(trailDir, "_trail.yaml"),
            "slug: lvl\nname: Level\nlevel: INTERMEDIATE\n");
        File.WriteAllText(Path.Combine(trailDir, "01.md"),
            "---\nslug: lvl-c\ntitle: T\norder: 1\nlevel: avancado\n---\nbody");

        await _sut.ImportAllAsync(_tempRoot);

        Assert.Equal(DifficultyLevel.Intermediate, (await _db.Trail.SingleAsync()).Level);
        Assert.Equal(DifficultyLevel.Advanced,     (await _db.Content.SingleAsync()).Level);
    }

    // ── helper ──────────────────────────────────────────────────────

    private void WriteTrail(string slug, string name, IEnumerable<(string Slug, string Title, string Body)> contents)
    {
        var dir = Path.Combine(_tempRoot, slug);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "_trail.yaml"),
            $"slug: {slug}\nname: {name}\ndescription: Test\nicon: 📘\nlevel: Beginner\n");

        var order = 1;
        foreach (var (cSlug, cTitle, cBody) in contents)
        {
            File.WriteAllText(Path.Combine(dir, $"{order:D2}-{cSlug}.md"),
                $"---\nslug: {cSlug}\ntitle: {cTitle}\norder: {order}\nlevel: Beginner\n---\n{cBody}");
            order++;
        }
    }
}
