namespace Unravel.Domain.Knowledge;

/// <summary>
/// Domínio acumulado de um usuário sobre um tópico específico. Atualizado a
/// cada tentativa do usuário em challenges que cobrem aquele tópico, via
/// <see cref="MasteryScoring"/>. Persistido por <c>(UserId, TopicId)</c>.
///
/// <para><b>Score</b> ∈ [0,1] é o nível "fresco" de domínio no momento de
/// <see cref="LastSeenAt"/>. Para o valor efetivo agora (com esquecimento),
/// usar <see cref="MasteryScoring.EffectiveScore"/>.</para>
///
/// <para><b>Confidence</b> é simplesmente o número de tentativas acumuladas
/// — não é probabilidade. Usado para escalar o α do EWMA (mais tentativas →
/// menos peso para uma única amostra) e para o planner decidir se já tem
/// dados suficientes pra tomar decisão (ex.: ignorar Mastery com n=1 no
/// matchmaking da Arena).</para>
///
/// <para><b>SrsInterval / EaseFactor</b>: agenda de revisão espaçada
/// (SM-2 simplificado). <see cref="NextDueAt"/> = LastSeenAt + SrsInterval
/// — quando vence, o planner sobe esse tópico na fila do dia.</para>
/// </summary>
public sealed class Mastery
{
    public Guid     UserId       { get; set; }
    public int      TopicId      { get; set; }
    public int      TrailId      { get; set; }
    public double   Score        { get; set; }
    public int      Confidence   { get; set; }
    public DateTime LastSeenAt   { get; set; }
    public int      SrsIntervalDays { get; set; }
    public double   EaseFactor   { get; set; }

    public DateTime NextDueAt => LastSeenAt.AddDays(SrsIntervalDays);

    /// <summary>Estado neutro para um par (user, topic) ainda não visto.
    /// Usado por <c>MasteryScoring.Apply</c> como ponto de partida quando o
    /// repositório retorna null. Não persiste por si só.</summary>
    public static Mastery Initial(Guid userId, int topicId, int trailId, DateTime asOf) => new()
    {
        UserId          = userId,
        TopicId         = topicId,
        TrailId         = trailId,
        Score           = 0.0,
        Confidence      = 0,
        LastSeenAt      = asOf,
        SrsIntervalDays = 1,
        EaseFactor      = 2.5,   // valor canônico do SM-2
    };
}
