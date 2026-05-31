using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Lê uma árvore de diretórios em <c>backend/knowledge/</c> e popula
/// Trail + Content via upsert por <c>Slug</c>. Cada subdiretório vira
/// uma trilha (com manifest <c>_trail.yaml</c>); cada arquivo
/// <c>NN-slug.md</c> com frontmatter YAML vira um Content.
///
/// <para>Estrutura esperada:</para>
/// <code>
/// backend/knowledge/
///   angular-fundamentos/
///     _trail.yaml
///     01-componentes.md
///     02-templates.md
///     ...
///   outra-trilha/
///     _trail.yaml
///     ...
/// </code>
///
/// <para>Operação <b>idempotente</b>: rodar duas vezes não duplica
/// nada — usa Slug como chave estável. Atualizações de Title, Body,
/// Level ou Order do MD são aplicadas no Content existente.
/// </para>
///
/// <para>Conteúdos que existiam no DB e <i>sumiram</i> do diretório
/// <b>NÃO são deletados</b> (proteção contra typo no path quebrar
/// dados de produção). Pra remover, marca <c>IsActive=false</c>
/// manualmente ou via futura PR de cleanup.</para>
/// </summary>
public sealed class KnowledgeImporter(
    ApplicationDbContext db,
    ILogger<KnowledgeImporter> log)
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Importa todas as trilhas encontradas em <paramref name="rootPath"/>.
    /// Retorna sumário pra logging/CLI.
    /// </summary>
    public async Task<ImportSummary> ImportAllAsync(string rootPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(rootPath))
        {
            log.LogWarning("Knowledge root '{Root}' não existe. Nada a importar.", rootPath);
            return new ImportSummary(0, 0, 0, 0);
        }

        var trailsCreated   = 0;
        var trailsUpdated   = 0;
        var contentsCreated = 0;
        var contentsUpdated = 0;

        foreach (var dir in Directory.EnumerateDirectories(rootPath).OrderBy(d => d))
        {
            var manifestPath = Path.Combine(dir, "_trail.yaml");
            if (!File.Exists(manifestPath))
            {
                log.LogDebug("Pulando '{Dir}': sem _trail.yaml.", dir);
                continue;
            }

            var summary = await ImportTrailAsync(dir, ct);
            if (summary.TrailWasCreated) trailsCreated++; else trailsUpdated++;
            contentsCreated += summary.ContentsCreated;
            contentsUpdated += summary.ContentsUpdated;
        }

        await db.SaveChangesAsync(ct);

        var result = new ImportSummary(trailsCreated, trailsUpdated, contentsCreated, contentsUpdated);
        log.LogInformation(
            "KnowledgeImporter concluído: trilhas {TC}/{TU}, conteúdos {CC}/{CU}.",
            trailsCreated, trailsUpdated, contentsCreated, contentsUpdated);
        return result;
    }

    /// <summary>
    /// Importa uma única trilha (diretório com <c>_trail.yaml</c>).
    /// Não chama SaveChanges — o caller (ImportAllAsync ou CLI) decide
    /// quando commitar pra permitir batching.
    /// </summary>
    public async Task<TrailImportSummary> ImportTrailAsync(string trailDir, CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(trailDir, "_trail.yaml");
        var manifest     = YamlDeserializer.Deserialize<TrailManifest>(await File.ReadAllTextAsync(manifestPath, ct));

        if (string.IsNullOrWhiteSpace(manifest.Slug))
            throw new InvalidOperationException($"'{manifestPath}' sem campo obrigatório 'slug'.");
        if (string.IsNullOrWhiteSpace(manifest.Name))
            throw new InvalidOperationException($"'{manifestPath}' sem campo obrigatório 'name'.");

        var trail = await db.Trail.FirstOrDefaultAsync(t => t.Slug == manifest.Slug, ct);
        var trailWasCreated = false;
        if (trail is null)
        {
            trail = new Trail { Slug = manifest.Slug };
            db.Trail.Add(trail);
            trailWasCreated = true;
        }
        trail.Name        = manifest.Name;
        trail.Description = manifest.Description ?? string.Empty;
        trail.Icon        = manifest.Icon        ?? "📘";
        trail.AccentColor = manifest.AccentColor ?? "#7038f2";
        trail.Level       = ParseLevel(manifest.Level);
        trail.IsActive    = true;

        // Forçar Id pra disponibilizar pro FK dos Contents (caso seja
        // trilha nova). Sem isso, o FK fica 0 e o save quebra.
        await db.SaveChangesAsync(ct);

        var created = 0;
        var updated = 0;

        foreach (var mdPath in Directory.EnumerateFiles(trailDir, "*.md").OrderBy(p => p))
        {
            var raw = await File.ReadAllTextAsync(mdPath, ct);
            var (frontmatter, body) = SplitFrontmatter(raw, mdPath);
            var doc = YamlDeserializer.Deserialize<ContentDocument>(frontmatter);

            if (string.IsNullOrWhiteSpace(doc.Slug))
                throw new InvalidOperationException($"'{mdPath}' sem campo obrigatório 'slug'.");
            if (string.IsNullOrWhiteSpace(doc.Title))
                throw new InvalidOperationException($"'{mdPath}' sem campo obrigatório 'title'.");

            var content = await db.Content.FirstOrDefaultAsync(c => c.Slug == doc.Slug, ct);
            if (content is null)
            {
                content = new Content { Slug = doc.Slug };
                db.Content.Add(content);
                created++;
            }
            else
            {
                updated++;
            }
            content.Title    = doc.Title;
            content.Body     = body.Trim();
            content.TrailId  = trail.Id;
            content.Order    = doc.Order;
            content.Level    = ParseLevel(doc.Level);
            content.Type     = ContentType.Article;
            content.IsActive = true;
            // ExternalUrl e CreatedAt não tocam — defaults já cobrem.
        }

        return new TrailImportSummary(trailWasCreated, created, updated);
    }

    internal static (string Frontmatter, string Body) SplitFrontmatter(string raw, string path)
    {
        // Aceita CRLF/LF/CR (Windows/Unix/old Mac).
        var normalized = raw.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!normalized.StartsWith("---\n"))
            throw new InvalidOperationException($"'{path}' não começa com '---' (frontmatter YAML obrigatório).");

        var end = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
            throw new InvalidOperationException($"'{path}' frontmatter não fechado com '---' em linha própria.");

        var frontmatter = normalized.Substring(4, end - 4);
        var body        = normalized[(end + 5)..];
        return (frontmatter, body);
    }

    private static DifficultyLevel ParseLevel(string? level) => (level ?? "Beginner").Trim().ToLowerInvariant() switch
    {
        "beginner"     or "iniciante"    => DifficultyLevel.Beginner,
        "intermediate" or "intermediario" or "intermediário" => DifficultyLevel.Intermediate,
        "advanced"     or "avancado"     or "avançado"       => DifficultyLevel.Advanced,
        _ => DifficultyLevel.Beginner,
    };
}

// ── DTOs do manifest YAML ────────────────────────────────────────────

/// <summary>Schema do <c>_trail.yaml</c>.</summary>
public sealed class TrailManifest
{
    public string  Slug         { get; set; } = string.Empty;
    public string  Name         { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public string? Icon         { get; set; }
    public string? AccentColor  { get; set; }
    public string? Level        { get; set; }
}

/// <summary>Schema do frontmatter de cada <c>NN-slug.md</c>.</summary>
public sealed class ContentDocument
{
    public string   Slug         { get; set; } = string.Empty;
    public string   Title        { get; set; } = string.Empty;
    public int      Order        { get; set; }
    public string?  Level        { get; set; }
    public string[] Tags         { get; set; } = Array.Empty<string>();
    public int      ReadMinutes  { get; set; }
}

public readonly record struct ImportSummary(
    int TrailsCreated, int TrailsUpdated,
    int ContentsCreated, int ContentsUpdated);

public readonly record struct TrailImportSummary(
    bool TrailWasCreated, int ContentsCreated, int ContentsUpdated);
