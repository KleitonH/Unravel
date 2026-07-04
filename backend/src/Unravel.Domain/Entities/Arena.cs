namespace Unravel.Domain.Entities;

/// <summary>Estado de uma partida da Arena (PvP).</summary>
public enum ArenaMatchStatus
{
    Pending  = 0, // desafio direto aguardando o oponente aceitar
    Active   = 1, // em andamento
    Finished = 2, // concluída (tem vencedor ou empate)
    Declined = 3, // desafio recusado
    Cancelled = 4, // cancelada (ex.: oponente saiu)
}

/// <summary>
/// Partida da Arena: dois alunos respondem a mesma sequência de questões
/// (snapshot da trilha-tema). Pontua por acerto + velocidade (mesmo
/// <c>LiveQuizScoring</c> do Quiz ao Vivo). Tempo real via SignalR; a fonte da
/// verdade é esta entidade. O NAVI de cada jogador o representa na batalha.
/// </summary>
public class ArenaMatch
{
    public int              Id        { get; set; }
    public Guid             Player1Id { get; set; } // desafiante / primeiro da fila
    public Guid?            Player2Id { get; set; } // oponente (definido ao parear/desafiar)
    public int              TrailId   { get; set; } // tema
    public ArenaMatchStatus Status    { get; set; } = ArenaMatchStatus.Pending;
    public Guid?            WinnerId  { get; set; } // null = empate (quando Finished) ou ainda indefinido
    public int              Score1    { get; set; }
    public int              Score2    { get; set; }

    public bool             IsDirectChallenge   { get; set; } // true=desafio; false=matchmaking
    public int              CurrentRoundIndex   { get; set; } = -1;
    public DateTime?        CurrentRoundStartedAt { get; set; }
    public int              SecondsPerQuestion  { get; set; } = 25;

    // ── HP / crítico (batalha por dano; vence por KO ou mais HP no teto) ──
    public int Hp1   { get; set; } = 100; // vida do jogador 1
    public int Hp2   { get; set; } = 100; // vida do jogador 2
    public int Crit1 { get; set; }        // cargas de crítico acumuladas (P1) — reforçam o próximo golpe
    public int Crit2 { get; set; }

    // ── desconexão / abandono (30s pra voltar, senão vence quem ficou) ──
    public Guid?     DisconnectedUserId { get; set; }
    public DateTime? DisconnectedAt     { get; set; }

    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt   { get; set; }

    public ICollection<ArenaRound> Rounds { get; set; } = new List<ArenaRound>();
}

/// <summary>Uma rodada/pergunta da partida, com as respostas dos dois jogadores.</summary>
public class ArenaRound
{
    public int     Id                   { get; set; }
    public int     MatchId              { get; set; }
    public int     OrderIndex           { get; set; }
    public int     GeneratedChallengeId { get; set; }
    public string  Prompt               { get; set; } = string.Empty;
    public string  OptionsJson          { get; set; } = "[]";
    public int     CorrectIndex         { get; set; }
    public string? Explanation          { get; set; }
    public string  Shape                { get; set; } = "MultipleChoice";

    public int? SelectedIndex1 { get; set; }
    public int? MsToAnswer1    { get; set; }
    public int  Points1        { get; set; }

    public int? SelectedIndex2 { get; set; }
    public int? MsToAnswer2    { get; set; }
    public int  Points2        { get; set; }

    // Dano sofrido por cada jogador nesta rodada (pro feedback do resultado).
    public int Damage1 { get; set; }
    public int Damage2 { get; set; }

    public ArenaMatch? Match { get; set; }
}

/// <summary>Placar acumulado da Arena por usuário (PK = UserId).</summary>
public class ArenaRanking
{
    public Guid UserId { get; set; }
    public int  Points { get; set; } // +3 vitória, +1 empate
    public int  Wins   { get; set; }
    public int  Losses { get; set; }
    public int  Draws  { get; set; }

    public User? User { get; set; }
}

/// <summary>Entrada na fila de matchmaking (1 por usuário).</summary>
public class ArenaQueueEntry
{
    public int      Id        { get; set; }
    public Guid     UserId    { get; set; }
    public int      TrailId   { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
