namespace Unravel.Domain.Entities;

/// <summary>
/// Ideia 1 — "Novelo de Lã / Parceiro de Trilha" (UC17). Mecânica social
/// colaborativa entre DOIS alunos: cada um cumpre sua meta diária para
/// "desenrolar" o novelo e passá-lo ao parceiro, criando um ciclo de
/// responsabilidade mútua.
///
/// ⚠️ Conflito de nomenclatura: "Novelo de Lã" também designa a moeda do
/// moderador (geração de questões por IA). Internamente esta mecânica é
/// chamada de <b>Parceria</b> / <b>Dupla</b> para evitar ambiguidade.
/// </summary>
public enum PartnershipStatus
{
    Pending   = 0, // convite enviado, aguardando o parceiro aceitar
    Active    = 1, // parceria ativa, novelo rolando
    Declined  = 2, // convite recusado
    Broken    = 3, // parceria desfeita por um dos dois
    Completed = 4, // chegaram ao fim de um ciclo de novelos (reservado)
}

/// <summary>Estado visual/lógico do novelo em andamento.</summary>
public enum YarnState
{
    Ok      = 0, // tudo em dia
    Tangled = 1, // "enrolado" — 1 dia sem cumprir a meta
    Dropped = 2, // "caído" — 2+ dias; o novelo cai e a parceria reinicia
}

/// <summary>Eventos registrados no histórico da parceria.</summary>
public enum PartnershipEventType
{
    Requested        = 0,
    Accepted         = 1,
    Passed           = 2, // novelo passou de um parceiro para o outro
    Tangled          = 3, // novelo enrolou (falha simples)
    Dropped          = 4, // novelo caiu (falha grave) — reinício
    NoveloCompleted  = 5, // um novelo inteiro foi concluído pela dupla
    Broken           = 6, // parceria desfeita
}

/// <summary>
/// Vínculo de parceria entre dois usuários. Armazenado UMA vez na direção
/// requester→addressee; o serviço trata o par como simétrico (A↔B).
/// </summary>
public class Partnership
{
    public int  Id          { get; set; }
    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }

    public PartnershipStatus Status     { get; set; } = PartnershipStatus.Pending;
    public DateTime          CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime?         AcceptedAt { get; set; }

    /// <summary>Quantos novelos inteiros a dupla já fechou junta.</summary>
    public int NovelosCompleted { get; set; }

    public User? Requester { get; set; }
    public User? Addressee { get; set; }

    public ICollection<YarnBall>        YarnBalls { get; set; } = new List<YarnBall>();
    public ICollection<PartnershipLog>  Logs      { get; set; } = new List<PartnershipLog>();
}

/// <summary>
/// O novelo em andamento. Há no máximo um ativo (IsCompleted=false) por
/// parceria. <see cref="CurrentOwnerId"/> é quem precisa cumprir a meta agora.
/// </summary>
public class YarnBall
{
    public int  Id             { get; set; }
    public int  PartnershipId  { get; set; }
    public Guid CurrentOwnerId { get; set; } // donoAtual — de quem é a vez

    public int Progress        { get; set; }            // progressoAtual no ciclo do dono atual
    public int DailyGoal       { get; set; } = 3;        // metaDia — quanto desenrola um ciclo
    public int CyclesCompleted { get; set; }            // trocas bem-sucedidas neste novelo
    public int TotalCycles     { get; set; } = 6;        // totalCiclos pra fechar o novelo

    public YarnState State     { get; set; } = YarnState.Ok;
    public bool      IsCompleted { get; set; }

    public DateTime  CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime? LastProgressAt { get; set; }

    public Partnership? Partnership { get; set; }
}

/// <summary>Histórico de trocas/falhas da parceria (auditoria + mural).</summary>
public class PartnershipLog
{
    public int                  Id            { get; set; }
    public int                  PartnershipId { get; set; }
    public Guid                 UserId        { get; set; } // ator do evento (quando aplicável)
    public PartnershipEventType Type          { get; set; }
    public string               Message       { get; set; } = string.Empty;
    public DateTime             CreatedAt     { get; set; } = DateTime.UtcNow;

    public Partnership? Partnership { get; set; }
}
