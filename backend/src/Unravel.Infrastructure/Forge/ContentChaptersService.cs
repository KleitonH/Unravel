using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Knowledge.Chunking;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// PR 60-a — Implementação canônica de <see cref="IContentChaptersService"/>.
///
/// <para><b>Algoritmo</b> (GetChaptersAsync):</para>
/// <list type="number">
///   <item>Carrega Content + body.</item>
///   <item>Roda <see cref="ChunkSegmenter"/> → lista ordenada de chunks por H2.</item>
///   <item>Carrega todas <c>GeneratedChallenge</c> ativas do content; agrupa
///     por <c>BodyJson.sourceChunkIndex</c> (índice no momento da geração).</item>
///   <item>Pra cada capítulo, calcula quota adaptativa:
///     <c>quota = clamp(min, pool.length, min + round(avgDifficulty * (max - min)))</c>.
///     Chunks fáceis recebem o mínimo; difíceis recebem até o máximo.</item>
///   <item>Ordena perguntas do capítulo por <c>EstimatedDifficulty</c> ascendente
///     (mais fácil primeiro — aluno entra "aquecendo"). Take(quota).</item>
/// </list>
/// </summary>
public sealed class ContentChaptersService(ApplicationDbContext db) : IContentChaptersService
{
    public async Task<ChapteredContentResponse?> GetChaptersAsync(
        int contentId, int minPerChapter, int maxPerChapter, CancellationToken ct = default)
    {
        var content = await db.Content.AsNoTracking()
            .Where(c => c.Id == contentId && c.IsActive)
            .Select(c => new { c.Id, c.Title, c.TrailId, c.Body })
            .FirstOrDefaultAsync(ct);
        if (content is null) return null;

        var chunks = new ChunkSegmenter().Segment(content.Body);

        // Carrega challenges ativas + extrai chunkIndex do BodyJson.
        var rawChallenges = await db.GeneratedChallenge.AsNoTracking()
            .Where(g => g.ContentId == contentId && g.IsActive)
            .ToListAsync(ct);
        var byChunk = GroupByChunk(rawChallenges);

        var chapterDtos = new List<ChapterDto>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var pool = byChunk.GetValueOrDefault(chunk.Index, new List<GeneratedChallenge>());
            var quota = AdaptiveQuota(pool, minPerChapter, maxPerChapter);
            var picked = pool
                .OrderBy(g => g.EstimatedDifficulty)
                .Take(quota)
                .Select(g => EntityToDto(g))
                .ToList();

            chapterDtos.Add(new ChapterDto(
                ChunkIndex: chunk.Index,
                Title:      ChapterTitle(chunk),
                BodyMd:     chunk.RawMarkdown,
                Challenges: picked));
        }

        return new ChapteredContentResponse(
            ContentId:     content.Id,
            ContentTitle:  content.Title,
            TrailId:       content.TrailId,
            MinPerChapter: minPerChapter,
            MaxPerChapter: maxPerChapter,
            Chapters:      chapterDtos);
    }

    public async Task<PublicationReadinessResponse?> GetReadinessAsync(
        int contentId, int requiredPerChapter, CancellationToken ct = default)
    {
        var content = await db.Content.AsNoTracking()
            .Where(c => c.Id == contentId && c.IsActive)
            .Select(c => new { c.Id, c.Body })
            .FirstOrDefaultAsync(ct);
        if (content is null) return null;

        var chunks   = new ChunkSegmenter().Segment(content.Body);
        var raw      = await db.GeneratedChallenge.AsNoTracking()
            .Where(g => g.ContentId == contentId && g.IsActive)
            .ToListAsync(ct);
        var byChunk  = GroupByChunk(raw);

        var rows = new List<ChapterReadiness>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var current = byChunk.GetValueOrDefault(chunk.Index, new List<GeneratedChallenge>()).Count;
            rows.Add(new ChapterReadiness(
                ChunkIndex: chunk.Index,
                Title:      ChapterTitle(chunk),
                Current:    current,
                Required:   requiredPerChapter,
                Ready:      current >= requiredPerChapter));
        }

        // Edge case: content sem H2 vira 1 capítulo "intro" (HeadingPath vazio).
        // Se chunks.Count == 0 (body vazio), considera "não pronto" sempre.
        var allReady = rows.Count > 0 && rows.All(r => r.Ready);

        return new PublicationReadinessResponse(
            ContentId:             contentId,
            Ready:                 allReady,
            MinRequiredPerChapter: requiredPerChapter,
            Chapters:              rows);
    }

    // ── helpers ────────────────────────────────────────────────────

    /// <summary>Agrupa challenges por sourceChunkIndex extraído do BodyJson.
    /// Rows sem o campo (pré-PR 34a) vão pro bucket 0 — comportamento legacy
    /// razoável; chunkIndex=0 é o primeiro chunk.</summary>
    private static Dictionary<int, List<GeneratedChallenge>> GroupByChunk(
        IEnumerable<GeneratedChallenge> all)
    {
        var dict = new Dictionary<int, List<GeneratedChallenge>>();
        foreach (var g in all)
        {
            var idx = ExtractChunkIndex(g.BodyJson) ?? 0;
            if (!dict.TryGetValue(idx, out var list))
            {
                list = new List<GeneratedChallenge>();
                dict[idx] = list;
            }
            list.Add(g);
        }
        return dict;
    }

    private static int? ExtractChunkIndex(string bodyJson)
    {
        if (string.IsNullOrWhiteSpace(bodyJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bodyJson);
            return doc.RootElement.TryGetProperty("sourceChunkIndex", out var el)
                && el.ValueKind == JsonValueKind.Number
                ? el.GetInt32()
                : null;
        }
        catch { return null; }
    }

    /// <summary>Calcula quanto pegar do pool, escalando entre min e max
    /// pela difficulty média (proxy de "complexidade do tópico"). Pool
    /// menor que min → retorna o pool inteiro (UI vai sinalizar gap).</summary>
    internal static int AdaptiveQuota(
        IReadOnlyList<GeneratedChallenge> pool, int min, int max)
    {
        if (pool.Count == 0) return 0;
        if (pool.Count <= min) return pool.Count;
        var avgDiff = pool.Average(g => Math.Clamp(g.EstimatedDifficulty, 0.0, 1.0));
        var span    = Math.Max(0, max - min);
        var extra   = (int)Math.Round(avgDiff * span);
        var quota   = Math.Clamp(min + extra, min, max);
        return Math.Min(quota, pool.Count);
    }

    private static string ChapterTitle(ContentChunk chunk)
    {
        // HeadingPath vazio = chunk de intro antes do primeiro H2.
        // Sub-chunk "Section > Sub" pega o último nível ("Sub").
        if (string.IsNullOrEmpty(chunk.HeadingPath)) return "Introdução";
        var parts = chunk.HeadingPath.Split(" > ");
        return parts[^1];
    }

    private static PoolChallengeDto EntityToDto(GeneratedChallenge g)
    {
        try
        {
            using var doc      = JsonDocument.Parse(g.BodyJson);
            var root           = doc.RootElement;
            var options        = root.GetProperty("options").EnumerateArray()
                                     .Select(e => e.GetString() ?? "").ToList();
            var correctIndex   = root.GetProperty("correctIndex").GetInt32();
            var explanation    = root.TryGetProperty("explanation", out var ex)
                                     ? ex.GetString() : null;
            var shape          = root.TryGetProperty("shape", out var sh)
                                     ? sh.GetString() ?? "MultipleChoice"
                                     : "MultipleChoice";
            return new PoolChallengeDto(
                g.Id, g.Strategy.ToString(), g.Prompt,
                options, correctIndex, explanation, g.EstimatedDifficulty, g.ContentId,
                Shape: shape);
        }
        catch (JsonException)
        {
            return new PoolChallengeDto(
                g.Id, g.Strategy.ToString(),
                "[pergunta corrompida; favor reportar]",
                new[] { "—" }, 0, null, g.EstimatedDifficulty, g.ContentId,
                Shape: "MultipleChoice");
        }
    }
}
