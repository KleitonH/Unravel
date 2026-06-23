namespace Unravel.Domain.Gamification;

/// <summary>
/// UC34 — recarga (refill) de vidas ao longo do tempo. Regra: +1 vida a cada
/// 1 hora, até o teto de <see cref="MaxLives"/>. Função pura e determinística
/// (recebe "agora") — a fonte da verdade do cap de vidas em todo o sistema.
/// </summary>
public static class LifeRefill
{
    /// <summary>Teto de vidas em todo o sistema (refill, login bônus, gameplay).</summary>
    public const int MaxLives = 7;

    /// <summary>Intervalo entre recargas: 1 vida por hora.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// Resultado do cálculo: novo total de vidas, nova âncora de tempo
    /// (quando a próxima vida começa a contar) e quantas vidas foram concedidas.
    /// </summary>
    public readonly record struct Result(int Lives, DateTime? Anchor, int Granted);

    /// <summary>
    /// Calcula a recarga. Não acumula tempo enquanto cheio (no teto a âncora vira
    /// "agora"); a primeira leitura com âncora nula apenas inicia o relógio.
    /// Nunca reduz vidas — se o usuário já estiver acima do teto (dado legado),
    /// mantém o valor e só zera o relógio.
    /// </summary>
    public static Result Compute(int currentLives, DateTime? lastRefillAt, DateTime nowUtc)
    {
        if (currentLives >= MaxLives)
            return new(currentLives, nowUtc, 0);

        if (lastRefillAt is null)
            return new(currentLives, nowUtc, 0); // inicia o relógio

        var anchor = lastRefillAt.Value;
        var hours  = (int)Math.Floor((nowUtc - anchor).TotalHours);
        if (hours <= 0)
            return new(currentLives, anchor, 0);

        var grant    = Math.Min(hours, MaxLives - currentLives);
        var newLives = currentLives + grant;
        // No teto: zera o relógio. Abaixo do teto: avança a âncora pelas horas
        // efetivamente consumidas, preservando o resto (minutos) pra próxima vida.
        var newAnchor = newLives >= MaxLives ? nowUtc : anchor.AddHours(grant);
        return new(newLives, newAnchor, grant);
    }
}
