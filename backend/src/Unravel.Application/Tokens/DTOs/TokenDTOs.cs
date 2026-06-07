namespace Unravel.Application.Tokens.DTOs;

/// <summary>PR 52 — saldo + classificação visual usada pelo frontend
/// pra escolher 1 de 5 tiers do &lt;YarnBall /&gt;.</summary>
public sealed record TokenBalanceResponse(
    int     BalanceCm,
    string  Tier,            // "Empty" | "Tiny" | "Small" | "Medium" | "Giant"
    string  DisplayBalance,  // ex "1m 87cm" ou "87 cm"
    int     EstimatedQuestionsRemaining   // BalanceCm × 0.69 yield
);

public sealed record TokenTransactionDto(
    long      Id,
    int       DeltaCm,
    string    Reason,
    string?   Metadata,
    string    CreatedAt    // ISO
);
