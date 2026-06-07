namespace Unravel.Application.Forge.Adaptive;

/// <summary>
/// PR 42 — algoritmo puro de Computerized Adaptive Testing (CAT-lite).
/// Estima o ability do aluno online a partir do histórico curto da
/// sessão e seleciona a próxima pergunta cujo <c>EstimatedDifficulty</c>
/// fica na zona proximal (ability + ε).
///
/// <para><b>Por que separado do use case</b>: a regra é puramente
/// matemática (sem DB, sem clock), permite testar com inputs sintéticos
/// e validar comportamento em casos limites (acerto seguido, erro
/// seguido, oscilação).</para>
///
/// <para><b>Modelo</b>: EWMA online com α decaindo por √(t+1). Inspirado
/// no <see cref="Unravel.Domain.Knowledge.MasteryScoring"/> (PR 2), mas
/// opera no escopo de UMA sessão de quiz, não no histórico permanente
/// do user.</para>
/// </summary>
public static class AdaptiveSelector
{
    /// <summary>Mínimo de perguntas antes de considerar parar por convergência.
    /// 3 é o mínimo razoável pra calcular variância significativa.</summary>
    public const int MinQuestions = 3;

    /// <summary>Máximo absoluto pra evitar sessão infinita. 7 é o sweet spot
    /// pra calibração razoável sem cansar o aluno (testes empíricos em CAT
    /// real apontam pra 5-10).</summary>
    public const int MaxQuestions = 7;

    /// <summary>Limiar de variância pra considerar convergência. σ² &lt; 0.0025
    /// ≈ desvio &lt; 0.05 — a ability variou menos que 5 pontos percentuais
    /// nas últimas 2 perguntas.</summary>
    public const double ConvergenceVarianceThreshold = 0.0025;

    /// <summary>Cold-start: ability inicial neutra. 0.3 é um beginner suave,
    /// alinhado com a heurística do JourneyPlanner.</summary>
    public const double DefaultStartAbility = 0.3;

    /// <summary>Offset da zona proximal. Pergunta um pouco mais difícil
    /// que o ability atual provoca aprendizado sem frustrar.</summary>
    public const double ProximalOffset = 0.10;

    /// <summary>Calcula o ability online via EWMA com α decaindo.
    /// α(t) = clamp(0.5 / √(1+t), 0.15, 0.5). Primeiras tentativas
    /// pesam mais; depois de 7-10 a contribuição marginal cai.</summary>
    public static double EstimateAbility(
        IReadOnlyList<AdaptiveOutcome> history,
        double startAbility = DefaultStartAbility)
    {
        var ability = startAbility;
        for (var i = 0; i < history.Count; i++)
        {
            var alpha   = Math.Clamp(0.5 / Math.Sqrt(1 + i), 0.15, 0.5);
            var outcome = history[i].WasCorrect ? 1.0 : 0.0;
            ability = alpha * outcome + (1 - alpha) * ability;
        }
        return ability;
    }

    /// <summary>Sequência completa de abilities pós-cada resposta. Útil pra
    /// detectar convergência (variance das últimas N) e pra UI mostrar trajetória.</summary>
    public static IReadOnlyList<double> AbilityTrajectory(
        IReadOnlyList<AdaptiveOutcome> history,
        double startAbility = DefaultStartAbility)
    {
        var trajectory = new List<double>(history.Count + 1) { startAbility };
        var current = startAbility;
        for (var i = 0; i < history.Count; i++)
        {
            var alpha   = Math.Clamp(0.5 / Math.Sqrt(1 + i), 0.15, 0.5);
            var outcome = history[i].WasCorrect ? 1.0 : 0.0;
            current = alpha * outcome + (1 - alpha) * current;
            trajectory.Add(current);
        }
        return trajectory;
    }

    /// <summary>Critério de parada: <c>true</c> quando a sessão deve encerrar.
    /// Composto de 3 regras com ordem de precedência:
    /// <list type="number">
    ///   <item>Atingiu <see cref="MaxQuestions"/> → para (cap absoluto).</item>
    ///   <item>Menos de <see cref="MinQuestions"/> → continua (precisa de
    ///   mais sinais).</item>
    ///   <item>Variância das últimas 2 abilities &lt;
    ///   <see cref="ConvergenceVarianceThreshold"/> → para (convergiu).</item>
    /// </list>
    /// </summary>
    public static AdaptiveStopReason? ShouldStop(IReadOnlyList<AdaptiveOutcome> history,
                                                 double startAbility = DefaultStartAbility)
    {
        if (history.Count >= MaxQuestions) return AdaptiveStopReason.MaxReached;
        if (history.Count < MinQuestions)  return null;

        var traj = AbilityTrajectory(history, startAbility);
        // Variância das últimas 2 transições (delta entre as 2 últimas abilities)
        var n = traj.Count;
        var last2 = new[] { traj[n - 1], traj[n - 2] };
        var mean = (last2[0] + last2[1]) / 2.0;
        var var_ = (Math.Pow(last2[0] - mean, 2) + Math.Pow(last2[1] - mean, 2)) / 2.0;
        return var_ < ConvergenceVarianceThreshold ? AdaptiveStopReason.Converged : null;
    }

    /// <summary>
    /// Seleciona a próxima pergunta entre os candidatos. Critério:
    /// minimizar |EstimatedDifficulty - target| onde target = ability +
    /// <see cref="ProximalOffset"/> (clamp [0.15, 0.90]). Tie-break por
    /// ServedCount asc + Id asc — determinístico e privilegia perguntas
    /// menos vistas (coleta mais sinal de CorrectRate).
    /// </summary>
    /// <returns>Id do challenge escolhido, ou <c>null</c> se candidates vazio.</returns>
    public static int? SelectNextChallengeId(
        double                              ability,
        IReadOnlyList<AdaptiveCandidate>    candidates,
        IReadOnlySet<int>                   excludeIds)
    {
        if (candidates.Count == 0) return null;

        var target  = Math.Clamp(ability + ProximalOffset, 0.15, 0.90);
        var pool    = candidates.Where(c => !excludeIds.Contains(c.Id)).ToList();
        if (pool.Count == 0) return null;

        return pool
            .OrderBy(c => Math.Abs(c.EstimatedDifficulty - target))
            .ThenBy(c => c.ServedCount)
            .ThenBy(c => c.Id)
            .First()
            .Id;
    }
}

/// <summary>Resultado de uma tentativa anterior na sessão. Mantido stateless
/// no servidor — cliente envia o histórico completo a cada request.</summary>
public sealed record AdaptiveOutcome(int ChallengeId, bool WasCorrect);

/// <summary>Visão mínima de uma <c>GeneratedChallenge</c> que o seletor precisa.
/// Permite testar sem DB.</summary>
public sealed record AdaptiveCandidate(int Id, double EstimatedDifficulty, int ServedCount);

public enum AdaptiveStopReason
{
    MaxReached = 1,
    Converged  = 2,
    PoolExhausted = 3,
}
