using Unravel.Domain.Forge;

namespace Unravel.Application.Forge.Ports;

/// <summary>Persistência das perguntas geradas. O use case do pool híbrido
/// é o consumidor primário; o job de cron noturno (PR futuro) é o
/// produtor primário, escrevendo em lote.</summary>
public interface IGeneratedChallengeRepository
{
    /// <summary>Tudo que existe pra um Content (ativo). Ordenado por
    /// <c>ServedCount</c> ascendente — preferimos servir perguntas menos
    /// vistas para coletar dados de <c>CorrectRate</c>.</summary>
    Task<IReadOnlyList<GeneratedChallenge>> GetByContentAsync(
        int contentId, CancellationToken ct = default);

    /// <summary>Insere em lote, retornando os IDs atribuídos. Usado pelo
    /// produtor do pool (gerador on-demand ou cron).</summary>
    Task AddManyAsync(IEnumerable<GeneratedChallenge> drafts, CancellationToken ct = default);

    /// <summary>Incrementa <c>ServedCount</c> dos challenges escolhidos
    /// pra evitar repetir os mesmos a cada request. Update atômico via
    /// SQL pra não exigir tracking.</summary>
    Task IncrementServedAsync(IEnumerable<int> challengeIds, CancellationToken ct = default);
}
