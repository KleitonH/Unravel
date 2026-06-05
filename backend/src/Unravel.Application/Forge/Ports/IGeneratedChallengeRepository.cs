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

    /// <summary>Busca uma pergunta gerada pelo Id. <c>null</c> se não existe
    /// ou está inativa. Usado pelo submit do quiz para validar a resposta
    /// contra o gabarito persistido (não confiamos no que o cliente envia).</summary>
    Task<GeneratedChallenge?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Após o usuário responder, registra o resultado: atualiza
    /// <c>CorrectRate</c> como média móvel sobre o <c>ServedCount</c> atual
    /// e incrementa <c>ServedCount</c> em 1. SQL atômico — evita race
    /// quando múltiplos users respondem a mesma pergunta simultaneamente.</summary>
    Task RecordOutcomeAsync(int challengeId, bool correct, CancellationToken ct = default);

    /// <summary>PR 37 — pool ativo filtrado por trail + lista de topics
    /// (fraquezas do user). Ordenado por <c>ServedCount</c> asc + <c>Id</c>
    /// (priorizar perguntas menos vistas, determinístico nos empates).</summary>
    Task<IReadOnlyList<GeneratedChallenge>> GetByTrailAndTopicsAsync(
        int trailId, IReadOnlyCollection<int> topicIds, CancellationToken ct = default);
}
