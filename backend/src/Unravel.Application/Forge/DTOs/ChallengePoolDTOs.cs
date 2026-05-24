namespace Unravel.Application.Forge.DTOs;

/// <summary>Pool de perguntas geradas para um Content, personalizado pelo
/// nível atual do usuário. Resposta do endpoint do PR 4.</summary>
public sealed record ChallengePoolResponse(
    int                                ContentId,
    string                             ContentTitle,
    int                                TrailId,
    double                             TargetUserMastery,
    IReadOnlyList<PoolChallengeDto>    Challenges
);

public sealed record PoolChallengeDto(
    int                  Id,
    string               Strategy,
    string               Prompt,
    IReadOnlyList<string> Options,
    int                  CorrectIndex,         // exposto p/ frontend exibir gabarito após resposta
    string?              Explanation,
    double               EstimatedDifficulty
);

// ── Submit (PR 13) ─────────────────────────────────────────────────────

/// <summary>Resposta enviada pelo cliente para validar e propagar o sinal
/// até a Mastery. ID veio do <see cref="PoolChallengeDto.Id"/>.</summary>
public sealed record SubmitPoolChallengeRequest(
    int GeneratedChallengeId,
    int SelectedOptionIndex
);

/// <summary>Resultado autoritativo: gabarito vem do servidor (cliente
/// nunca decide se acertou), explicação, e atualização visível da
/// mastery do tópico para a UI mostrar feedback.</summary>
public sealed record SubmitPoolChallengeResponse(
    bool    IsCorrect,
    int     CorrectOptionIndex,
    string? Explanation,
    double  NewMasteryScore,
    int     NewMasteryConfidence
);
