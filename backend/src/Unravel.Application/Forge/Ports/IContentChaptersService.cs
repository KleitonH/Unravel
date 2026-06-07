using Unravel.Application.Forge.DTOs;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// PR 60-a — Serviço que combina <c>ChunkSegmenter</c> (markdown H2 →
/// chunks) com pool de <c>GeneratedChallenge</c> pra entregar conteúdo
/// fatiado em capítulos didáticos.
///
/// <para>Implementação canônica em <c>Infrastructure.Forge.ContentChaptersService</c>.
/// Separado em port porque o use case admin (publication-readiness)
/// também consome.</para>
/// </summary>
public interface IContentChaptersService
{
    /// <summary>Carrega conteúdo + chapters do moderador autenticado pra
    /// servir ao aluno. Aplica alocação adaptativa de perguntas por
    /// capítulo (<paramref name="minPerChapter"/>..<paramref name="maxPerChapter"/>)
    /// baseada na difficulty média do chunk.
    /// Retorna <c>null</c> se content não existe ou está inativo.</summary>
    Task<ChapteredContentResponse?> GetChaptersAsync(
        int contentId,
        int minPerChapter,
        int maxPerChapter,
        CancellationToken ct = default);

    /// <summary>Inspeciona se o content tem perguntas suficientes em
    /// CADA capítulo pra ser publicado. <paramref name="requiredPerChapter"/>
    /// default 4 (decisão PR 60-a).</summary>
    Task<PublicationReadinessResponse?> GetReadinessAsync(
        int contentId,
        int requiredPerChapter,
        CancellationToken ct = default);
}
