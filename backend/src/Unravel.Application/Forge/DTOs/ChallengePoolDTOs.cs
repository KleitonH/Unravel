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
