namespace Unravel.Domain.Entities;

/// <summary>Modo de entrada do Quiz ao Vivo.</summary>
public enum LiveQuizMode
{
    Turma = 0, // só alunos selecionados de turmas do professor (whitelist)
    Livre = 1, // qualquer usuário autenticado com o código
}

public enum LiveQuizStatus
{
    Lobby    = 0, // criado, aguardando início (alunos entrando)
    Running  = 1, // em andamento
    Finished = 2, // encerrado
}

/// <summary>
/// Sessão de Quiz ao Vivo (estilo Kahoot) hospedada por um professor.
/// Snapshot das perguntas escolhidas + participantes + respostas. Pontuação
/// por acerto + velocidade. Entrada gated por turma (whitelist) ou por código
/// livre. SignalR empurra o tempo real; a fonte da verdade é esta sessão.
/// </summary>
public class LiveQuizSession
{
    public int            Id          { get; set; }
    public Guid           HostUserId  { get; set; }
    public LiveQuizMode   Mode        { get; set; }
    public string         JoinCode    { get; set; } = string.Empty; // código curto da sala
    public LiveQuizStatus Status      { get; set; } = LiveQuizStatus.Lobby;

    /// <summary>-1 = ainda no lobby; 0..N-1 = pergunta atual.</summary>
    public int            CurrentQuestionIndex { get; set; } = -1;
    /// <summary>Quando a pergunta atual começou (pro cálculo de velocidade).</summary>
    public DateTime?      CurrentQuestionStartedAt { get; set; }

    public int            SecondsPerQuestion { get; set; } = 20;
    public bool           ShowRankBetween    { get; set; } = true;

    public DateTime       CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime?      StartedAt   { get; set; }
    public DateTime?      EndedAt     { get; set; }

    public ICollection<LiveQuizQuestion>    Questions    { get; set; } = new List<LiveQuizQuestion>();
    public ICollection<LiveQuizParticipant> Participants { get; set; } = new List<LiveQuizParticipant>();
    public ICollection<LiveQuizAllowedUser> AllowedUsers { get; set; } = new List<LiveQuizAllowedUser>();
}

/// <summary>Snapshot de uma pergunta na sessão (já na ordem/embaralhamento final).</summary>
public class LiveQuizQuestion
{
    public int     Id                   { get; set; }
    public int     SessionId            { get; set; }
    public int     OrderIndex           { get; set; }
    public int     GeneratedChallengeId { get; set; } // origem (rastreio)
    public string  Prompt               { get; set; } = string.Empty;
    public string  OptionsJson          { get; set; } = "[]"; // array JSON de strings
    public int     CorrectIndex         { get; set; }
    public string? Explanation          { get; set; }
    public string  Shape                { get; set; } = "MultipleChoice";

    public LiveQuizSession? Session { get; set; }
}

/// <summary>Whitelist de quem pode entrar (modo turma).</summary>
public class LiveQuizAllowedUser
{
    public int  Id        { get; set; }
    public int  SessionId { get; set; }
    public Guid UserId    { get; set; }

    public LiveQuizSession? Session { get; set; }
}

/// <summary>Participante conectado à sessão.</summary>
public class LiveQuizParticipant
{
    public int      Id          { get; set; }
    public int      SessionId   { get; set; }
    public Guid     UserId      { get; set; }
    public string   DisplayName { get; set; } = string.Empty;
    public int      Score       { get; set; }
    public DateTime JoinedAt    { get; set; } = DateTime.UtcNow;

    public LiveQuizSession? Session { get; set; }
}

/// <summary>Resposta de um participante a uma pergunta da sessão.</summary>
public class LiveQuizAnswer
{
    public int  Id                 { get; set; }
    public int  SessionId          { get; set; }
    public int  ParticipantId      { get; set; }
    public int  QuestionOrderIndex { get; set; }
    public int  SelectedIndex      { get; set; }
    public bool IsCorrect          { get; set; }
    public int  MsToAnswer         { get; set; }
    public int  Points             { get; set; }
    public DateTime AnsweredAt      { get; set; } = DateTime.UtcNow;
}
