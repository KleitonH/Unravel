namespace Unravel.Application.Achievements.Ports;

/// <summary>
/// Títulos desbloqueáveis (Ideia 5) + ranking global. O título ativo aparece
/// junto ao nome do usuário. Motor de concessão por critério (ofensiva, arena,
/// XP). "Tags" visuais continuam no sistema de Badge existente.
/// </summary>

public record TitleDto(
    int    Id,
    string Text,
    string Category,
    string Criterion,
    int    Threshold,
    bool   Owned,
    bool   Active);

public record GlobalRankingRow(int Rank, Guid UserId, string Name, int Xp, string? ActiveTitle);

public enum ActivateTitleOutcome { Ok, NotFound, NotOwned }

public interface ITitleService
{
    /// <summary>Catálogo de títulos com flags <c>owned</c>/<c>active</c> pro usuário.</summary>
    Task<IReadOnlyList<TitleDto>> ListAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Ativa um título que o usuário possui (vira o ActiveTitle); null/0 limpa.</summary>
    Task<ActivateTitleOutcome> ActivateAsync(Guid userId, int titleId, CancellationToken ct = default);

    /// <summary>Concede os títulos cujos critérios o usuário já cumpre (idempotente). Retorna os novos.</summary>
    Task<IReadOnlyList<string>> EvaluateAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Ranking global por XP acumulado.</summary>
    Task<IReadOnlyList<GlobalRankingRow>> GlobalRankingAsync(int top, CancellationToken ct = default);
}
