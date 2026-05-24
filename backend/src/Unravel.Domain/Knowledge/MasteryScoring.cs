namespace Unravel.Domain.Knowledge;

/// <summary>
/// Funções <i>puras</i> que atualizam e consultam <see cref="Mastery"/>.
/// Sem dependência de DB, clock ou aleatoriedade — recebem tudo por
/// parâmetro. Isso permite testar cada regra (EWMA, SM-2, decaimento) com
/// inputs sintéticos e deixa o repositório como mero "saver".
///
/// <para>Por que separar daqui da entidade: a entidade é o estado; estas são
/// as transições. Manter as transições puras evita acoplamentos (ex.: chamar
/// <c>DateTime.UtcNow</c> dentro da entidade quebraria testes).</para>
/// </summary>
public static class MasteryScoring
{
    /// <summary>Meia-vida do esquecimento em dias — após esse tempo sem
    /// contato, o <see cref="Mastery.Score"/> efetivo cai para metade.
    /// 14 dias é um meio-termo conservador entre Ebbinghaus original (~1
    /// dia) e estimativas para conteúdo aprendido com prática.</summary>
    public const double ForgettingHalfLifeDays = 14.0;

    private static readonly double ForgettingLambda = Math.Log(2) / ForgettingHalfLifeDays;

    /// <summary>α do EWMA decai com a confiança: muitas tentativas → menos
    /// peso para uma única amostra nova. Piso de 0.15 evita que o score
    /// "congele" depois de N tentativas; teto de 0.5 evita que a 1ª
    /// tentativa apague tudo.</summary>
    private static double Alpha(int confidence) =>
        Math.Clamp(0.5 / Math.Sqrt(1 + confidence), 0.15, 0.5);

    /// <summary>Aplica uma nova tentativa a um <see cref="Mastery"/>,
    /// retornando uma cópia atualizada. Não muta o input — a função é pura.</summary>
    /// <param name="current">Estado atual (use <see cref="Mastery.Initial"/>
    /// se for o primeiro contato).</param>
    /// <param name="outcome">Razão de acerto da tentativa em [0,1].</param>
    /// <param name="asOf">Quando a tentativa ocorreu — usado para
    /// <see cref="Mastery.LastSeenAt"/>.</param>
    public static Mastery Apply(Mastery current, double outcome, DateTime asOf)
    {
        if (outcome is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "outcome must be in [0,1]");

        var alpha    = Alpha(current.Confidence);
        var newScore = alpha * outcome + (1 - alpha) * current.Score;

        var (interval, ease) = NextSrs(current.SrsIntervalDays, current.EaseFactor, outcome);

        return new Mastery
        {
            UserId          = current.UserId,
            TopicId         = current.TopicId,
            TrailId         = current.TrailId,
            Score           = newScore,
            Confidence      = current.Confidence + 1,
            LastSeenAt      = asOf,
            SrsIntervalDays = interval,
            EaseFactor      = ease,
        };
    }

    /// <summary>SM-2 simplificado em três faixas:
    /// <list type="bullet">
    ///   <item><b>≥ 0.7</b> (bom): intervalo × ease, ease += 0.1 (cap 2.8).</item>
    ///   <item><b>0.4..0.7</b> (parcial): intervalo mantém, ease intocado.</item>
    ///   <item><b>&lt; 0.4</b> (ruim): intervalo volta para 1 dia, ease -= 0.2 (piso 1.3).</item>
    /// </list>
    /// Não fazemos a fórmula completa de Anki (quality 0..5) porque
    /// challenge da plataforma já dá uma razão contínua — chega.</summary>
    private static (int interval, double ease) NextSrs(int interval, double ease, double outcome)
    {
        if (outcome >= 0.7)
        {
            var newEase = Math.Min(2.8, ease + 0.1);
            var next    = interval <= 1 ? 1 : (int)Math.Ceiling(interval * newEase);
            // Primeira tentativa boa promove a 3 dias diretamente — Leitner clássico.
            if (interval == 1) next = 3;
            return (next, newEase);
        }
        if (outcome >= 0.4)
        {
            return (Math.Max(1, interval), ease);
        }
        return (1, Math.Max(1.3, ease - 0.2));
    }

    /// <summary>Score com decaimento por esquecimento desde
    /// <see cref="Mastery.LastSeenAt"/>. Modela curva exponencial clássica
    /// (Ebbinghaus): cada <see cref="ForgettingHalfLifeDays"/> sem contato
    /// reduz o score à metade.</summary>
    public static double EffectiveScore(Mastery mastery, DateTime asOf)
    {
        var days = Math.Max(0, (asOf - mastery.LastSeenAt).TotalDays);
        return mastery.Score * Math.Exp(-ForgettingLambda * days);
    }

    /// <summary>true se o tópico já passou da data sugerida de revisão. Usado
    /// pelo planner para priorizar revisões atrasadas na fila do dia.</summary>
    public static bool IsDueForReview(Mastery mastery, DateTime asOf) =>
        asOf >= mastery.NextDueAt;
}
