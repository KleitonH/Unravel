namespace Unravel.Application.Forge.DTOs;

/// <summary>PR 50 — payload do <c>POST /trails/{id}/boss-fight/start</c>.
/// Backend valida desbloqueio (todas as ilhas regulares completadas)
/// e retorna 10 perguntas balanceadas + estado atual do user.</summary>
public sealed record BossFightStartResponse(
    int                              TrailId,
    string                           TrailName,
    bool                             Unlocked,
    string?                          LockReason,         // null se Unlocked=true
    int                              PassThreshold,      // ex 7 (em N=10 → 70%)
    int                              TotalQuestions,
    int                              AttemptCount,       // do user nessa trilha
    int                              BestScore,
    DateTime?                        FirstWonAt,
    IReadOnlyList<PoolChallengeDto>  Questions
);

/// <summary>Submit das respostas da sessão Boss Fight (batch — tudo de
/// uma vez, não streaming como CAT). Cada resposta carrega challengeId +
/// índice selecionado pelo aluno.</summary>
public sealed record BossFightSubmitRequest(
    IReadOnlyList<BossFightAnswer> Answers
);

public sealed record BossFightAnswer(
    int ChallengeId,
    int SelectedOptionIndex
);

/// <summary>Resultado autoritativo: backend valida cada resposta, atualiza
/// mastery por topic, registra UserSeenChallenge, persiste UserBossFight,
/// e devolve recompensas pro front mostrar tela de vitória/derrota.</summary>
public sealed record BossFightResultResponse(
    int                                TrailId,
    int                                Score,            // acertos / N
    int                                TotalQuestions,
    int                                PassThreshold,
    bool                               Passed,
    bool                               IsFirstWin,
    int                                XpEarned,
    string?                            BadgeAwarded,    // ex "Mestre de Angular Fundamentos"
    IReadOnlyList<BossFightAnswerOutcome> Outcomes
);

public sealed record BossFightAnswerOutcome(
    int     ChallengeId,
    bool    IsCorrect,
    int     CorrectOptionIndex,
    string? Explanation
);
