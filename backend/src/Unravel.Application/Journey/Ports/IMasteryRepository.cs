using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Persistência de <see cref="Mastery"/>. Chave composta (UserId, TopicId);
/// <c>TrailId</c> é redundante (TopicId já implica trilha) mas existe na
/// linha para indexação por trilha sem JOIN — escrita mais cara, leitura
/// muito mais barata, que é o padrão de acesso dominante.
/// </summary>
public interface IMasteryRepository
{
    /// <summary>Lê um par específico. Retorna null se nunca visto — o
    /// chamador deve materializar via <see cref="Mastery.Initial"/>.</summary>
    Task<Mastery?> GetAsync(Guid userId, int topicId, CancellationToken ct = default);

    /// <summary>Todas as mastery do usuário numa trilha. Usado pelo planner
    /// para montar a foto do dia e pelo endpoint de relatório.</summary>
    Task<IReadOnlyList<Mastery>> GetByTrailAsync(Guid userId, int trailId, CancellationToken ct = default);

    /// <summary>Tópicos cuja revisão venceu até <paramref name="asOf"/>.
    /// O planner consulta isso para empurrar revisões na fila do dia
    /// antes de novos conteúdos.</summary>
    Task<IReadOnlyList<Mastery>> GetDueForReviewAsync(
        Guid userId, int trailId, DateTime asOf, CancellationToken ct = default);

    /// <summary>Insere ou atualiza. Idempotente. Deve persistir com
    /// SaveChangesAsync embutido — o chamador típico atualiza N masteries
    /// numa única transação lógica e não deveria precisar gerenciar o
    /// DbContext diretamente.</summary>
    Task UpsertAsync(Mastery mastery, CancellationToken ct = default);

    /// <summary>Upsert em lote. Importante para o hook do ChallengeService
    /// que pode tocar vários topics numa única submissão.</summary>
    Task UpsertManyAsync(IEnumerable<Mastery> masteries, CancellationToken ct = default);
}
