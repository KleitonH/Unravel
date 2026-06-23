namespace Unravel.Application.LiveQuiz.Ports;

/// <summary>
/// UC30 — Relatório pedagógico da turma. Após uma sessão de Quiz ao Vivo, o
/// professor consulta o desempenho: acertos por questão, lacunas por tópico
/// (conteúdo de origem) e desempenho individual/agregado. Lê os dados já
/// persistidos da sessão (respostas por participante/pergunta).
/// </summary>

/// <summary>Item da lista de sessões do professor (pra seleção do relatório).</summary>
public record LiveQuizSessionListItem(
    int       Id,
    string    JoinCode,
    string    Mode,
    string    Status,
    DateTime  CreatedAt,
    DateTime? EndedAt,
    int       ParticipantCount,
    int       QuestionCount);

public record LiveQuizReportSummary(
    int       SessionId,
    string    JoinCode,
    string    Mode,
    string    Status,
    DateTime? EndedAt,
    int       ParticipantCount,
    int       QuestionCount,
    int       AnswersCount,
    double    OverallAccuracy, // 0..1 (acertos / respostas)
    double    AvgScore);

/// <summary>Desempenho por questão (acertos, tempo médio, distribuição de opções).</summary>
public record LiveQuizReportQuestionRow(
    int                  OrderIndex,
    string               Prompt,
    int                  CorrectIndex,
    int                  TotalAnswers,
    int                  CorrectCount,
    double               Accuracy,            // 0..1
    int                  AvgMs,
    IReadOnlyList<int>   OptionDistribution); // contagem por índice de opção

/// <summary>Desempenho individual de cada participante.</summary>
public record LiveQuizReportParticipantRow(
    int    Rank,
    Guid   UserId,
    string DisplayName,
    int    Score,
    int    Answered,
    int    CorrectCount,
    double Accuracy);

/// <summary>Lacuna por tópico (conteúdo de origem das perguntas).</summary>
public record LiveQuizReportTopicRow(
    string Topic,
    int    TotalAnswers,
    int    CorrectCount,
    double Accuracy);

public record LiveQuizReportDto(
    LiveQuizReportSummary                       Summary,
    IReadOnlyList<LiveQuizReportQuestionRow>    Questions,
    IReadOnlyList<LiveQuizReportParticipantRow> Participants,
    IReadOnlyList<LiveQuizReportTopicRow>       Topics);

public interface ILiveQuizReportService
{
    /// <summary>Sessões hospedadas pelo professor (mais recentes primeiro).</summary>
    Task<IReadOnlyList<LiveQuizSessionListItem>> ListHostSessionsAsync(Guid hostUserId, CancellationToken ct = default);

    /// <summary>Relatório de uma sessão. null se não existe ou não é do professor.</summary>
    Task<LiveQuizReportDto?> GetReportAsync(Guid hostUserId, int sessionId, CancellationToken ct = default);
}
