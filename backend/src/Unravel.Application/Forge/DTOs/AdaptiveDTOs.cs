namespace Unravel.Application.Forge.DTOs;

/// <summary>
/// PR 42 — request stateless do CAT. Cliente envia histórico curto a cada
/// chamada (não mantemos sessão no servidor).
/// </summary>
public sealed record AdaptiveNextRequest(
    IReadOnlyList<AdaptiveHistoryItem> History
);

/// <summary>Item do histórico da sessão CAT — id da pergunta + se acertou.</summary>
public sealed record AdaptiveHistoryItem(
    int  ChallengeId,
    bool WasCorrect
);

/// <summary>
/// Resposta do endpoint <c>POST /adaptive/next</c>. Quando <c>done=true</c>,
/// <c>question</c> é null e <c>stopReason</c> indica por quê.
/// </summary>
public sealed record AdaptiveNextResponse(
    bool                Done,
    string?             StopReason,        // "MaxReached" | "Converged" | "PoolExhausted" | null
    double              AbilityEstimate,
    PoolChallengeDto?   Question,
    int                 QuestionsAnswered
);
