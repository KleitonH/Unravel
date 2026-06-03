using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Eval;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge.Eval;

/// <summary>
/// Lê itens de gold curados por moderador (PR 33d) da tabela
/// <c>ModeratorGoldItem</c> e converte pro mesmo schema do
/// <see cref="GoldItem"/> usado pelo evaluator. Permite que o
/// <see cref="ForgeEvaluator"/> trate as duas fontes (YAML + DB)
/// de forma uniforme.
///
/// <para>Items com <c>DistractorsJson</c> malformado, com !=3
/// distratores, ou sem campos obrigatórios são silenciosamente
/// filtrados — mesma tolerância do <see cref="GoldSetReader"/> pra
/// placeholders TODO.</para>
/// </summary>
public static class ModeratorGoldReader
{
    /// <summary>
    /// Lê todos os items ativos pertencentes ao trail do gold YAML,
    /// resolvendo o Content via Slug. Retorna lista filtrada.
    /// </summary>
    public static async Task<IReadOnlyList<GoldItem>> ReadForTrailAsync(
        ApplicationDbContext db,
        string               trailSlug,
        CancellationToken    ct = default)
    {
        if (string.IsNullOrWhiteSpace(trailSlug)) return Array.Empty<GoldItem>();

        // Query em 2 etapas pra contornar limitações do EF InMemory
        // com chained navigation (g.Content.Trail.Slug). No Postgres
        // o EF gera JOIN normalmente; aqui mantemos a mesma forma
        // pra consistência entre prod e testes.
        // Etapa 0: TrailId pelo slug (separado pra ser InMemory-compatível)
        var trailId = await db.Trail
            .AsNoTracking()
            .Where(t => t.Slug == trailSlug)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (trailId is null) return Array.Empty<GoldItem>();

        // Etapa 1: ContentIds da trilha com slug não-nulo
        var contentMap = await db.Content
            .AsNoTracking()
            .Where(c => c.TrailId == trailId.Value && c.Slug != null)
            .Select(c => new { c.Id, c.Slug })
            .ToDictionaryAsync(c => c.Id, c => c.Slug!, ct);

        if (contentMap.Count == 0) return Array.Empty<GoldItem>();

        var contentIds = contentMap.Keys.ToList();

        var rows = await db.ModeratorGoldItem
            .AsNoTracking()
            .Where(g => g.IsActive && contentIds.Contains(g.ContentId))
            .Select(g => new
            {
                g.ContentId,
                g.SourceClaim,
                g.Prompt,
                g.CorrectAnswer,
                g.DistractorsJson,
                g.Explanation,
                g.DifficultyHint,
            })
            .ToListAsync(ct);

        var result = new List<GoldItem>(rows.Count);
        foreach (var r in rows)
        {
            if (!contentMap.TryGetValue(r.ContentId, out var contentSlug)) continue;
            if (string.IsNullOrWhiteSpace(contentSlug)) continue;

            List<string>? distractors;
            try
            {
                distractors = JsonSerializer.Deserialize<List<string>>(r.DistractorsJson);
            }
            catch (JsonException)
            {
                continue; // distratores malformados → skip
            }
            if (distractors is null || distractors.Count != 3) continue;
            if (distractors.Any(string.IsNullOrWhiteSpace)) continue;

            // Validação inline mais lax que GoldItem.IsCompleted():
            // SourceClaim é OPCIONAL no contexto moderator (gold YAML
            // exige; gold DB curado a mão pode não ter).
            if (string.IsNullOrWhiteSpace(r.Prompt))         continue;
            if (string.IsNullOrWhiteSpace(r.CorrectAnswer))  continue;

            result.Add(new GoldItem
            {
                TopicSlug      = contentSlug,
                SourceClaim    = r.SourceClaim    ?? string.Empty,
                Prompt         = r.Prompt,
                CorrectAnswer  = r.CorrectAnswer,
                Distractors    = distractors,
                Explanation    = r.Explanation    ?? string.Empty,
                DifficultyHint = r.DifficultyHint ?? 0.50,
            });
        }

        return result;
    }
}
