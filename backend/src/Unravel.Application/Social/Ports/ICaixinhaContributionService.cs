namespace Unravel.Application.Social.Ports;

/// <summary>
/// PR 67 — conecta o estudo individual à meta coletiva da caixinha. Chamado no
/// write-path de recompensas (submit do quiz): o XP ganho soma nos pontos do
/// dia da caixinha; ao bater a meta, todos os membros ganham bônus de moedas.
/// </summary>
public interface ICaixinhaContributionService
{
    /// <summary>Credita `xpEarned` na meta diária da caixinha do usuário (no-op
    /// se ele não tem caixinha ou xp &lt;= 0).</summary>
    Task ContributeAsync(Guid userId, int xpEarned, DateTime now, CancellationToken ct = default);
}
