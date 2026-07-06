namespace Unravel.Domain.Forge;

/// <summary>
/// Tipo do problema apontado pelo aluno numa pergunta. Taxonomia fechada
/// (fora <see cref="Outro"/>) pra o moderador triar rápido sem ler texto
/// livre em todos os casos.
/// </summary>
public enum FeedbackReason
{
    /// <summary>O gabarito parece errado (a alternativa marcada como certa não é).</summary>
    GabaritoErrado  = 0,
    /// <summary>Enunciado ambíguo ou confuso.</summary>
    Ambigua         = 1,
    /// <summary>Mais de uma alternativa está correta.</summary>
    MultiplaCorreta = 2,
    /// <summary>Assunto fora do conteúdo estudado.</summary>
    ForaDoConteudo  = 3,
    /// <summary>Outro problema — exige comentário livre.</summary>
    Outro           = 4,
}

/// <summary>Ciclo de vida do feedback na fila de moderação.</summary>
public enum FeedbackStatus
{
    /// <summary>Aguardando revisão do moderador.</summary>
    Aberto     = 0,
    /// <summary>Moderador revisou e agiu (corrigiu/desativou a pergunta).</summary>
    Revisado   = 1,
    /// <summary>Moderador avaliou e considerou improcedente.</summary>
    Descartado = 2,
}

/// <summary>
/// Sinalização de um aluno de que uma <see cref="GeneratedChallenge"/> está
/// inadequada. Guarda quem sinalizou, o tipo do problema e (opcional) um
/// comentário. Fica em fila (<see cref="FeedbackStatus.Aberto"/>) até o
/// moderador triar.
///
/// <para><b>Por que persistir e não só telemetria</b>: o moderador precisa
/// agir sobre a pergunta específica (corrigir gabarito, desativar) e ter
/// rastreabilidade (qual aluno, quando, quantos reportaram o mesmo). Um
/// contador agregado não permitiria a triagem individual.</para>
///
/// <para><see cref="ContentId"/> é desnormalizado da challenge pra permitir
/// listar os feedbacks de um conteúdo inteiro (painel do moderador) sem
/// join. Índice único (challenge, user) impede um aluno de inflar a
/// contagem sinalizando a mesma pergunta várias vezes.</para>
/// </summary>
public sealed class ChallengeFeedback
{
    public int            Id                   { get; set; }
    public int            GeneratedChallengeId { get; set; }
    public int            ContentId            { get; set; }
    public Guid           UserId               { get; set; }
    public FeedbackReason Reason               { get; set; }
    public string?        Comment              { get; set; }
    public FeedbackStatus Status               { get; set; } = FeedbackStatus.Aberto;
    public DateTime       CreatedAt            { get; set; } = DateTime.UtcNow;

    /// <summary>Moderador que triou (null enquanto Aberto).</summary>
    public Guid?          ReviewedByUserId     { get; set; }
    public DateTime?      ReviewedAt           { get; set; }
}
