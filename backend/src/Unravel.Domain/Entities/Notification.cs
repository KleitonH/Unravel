namespace Unravel.Domain.Entities;

/// <summary>PR 69 — central de notificações in-app (hábito/engajamento).</summary>
public enum NotificationType
{
    System          = 0,
    FriendRequest   = 1,
    FriendAccepted  = 2,
    CaixinhaGoal    = 3,
    CaixinhaStreak  = 4,
    LeaguePromoted  = 5,
    LeagueRelegated = 6,
    EventStarted    = 7,
    StreakRisk      = 8,
    ClassInvite     = 9,  // convite pra entrar numa turma de um professor
    LiveQuizStarted = 10, // professor abriu um Quiz ao Vivo pra turma
    ArenaChallenge  = 11, // desafio/duelo na Arena (PvP)
    PartnershipRequest  = 12, // convite de Parceria (Novelo / Parceiro de Trilha)
    PartnershipAccepted = 13, // parceiro aceitou o convite
    YarnPassed          = 14, // o novelo passou pra você (ou novelo concluído)
    Welcome             = 15, // brinde de boas-vindas / pré-registro (set Mestre dos Gatos)
}

public class Notification
{
    public int              Id        { get; set; }
    public Guid             UserId    { get; set; }
    public NotificationType Type      { get; set; }
    public string           Title     { get; set; } = string.Empty;
    public string           Body      { get; set; } = string.Empty;
    public string?          Link      { get; set; } // rota in-app (ex: "/amigos")
    public bool             IsRead    { get; set; }
    public DateTime         CreatedAt { get; set; } = DateTime.UtcNow;
}
