namespace Unravel.Application.Journey.DTOs;

/// <summary>Resposta serializada para o frontend. Estrutura espelha
/// <c>JourneyPlan</c> mas só com tipos primitivos / strings — facilita
/// versionar contrato HTTP sem amarrar ao Domain.</summary>
public sealed record JourneyPlanResponse(
    Guid                            UserId,
    int                             TrailId,
    string                          TrailName,
    DateTime                        GeneratedAt,
    int                             MetaDia,
    IReadOnlyList<JourneyItemDto>   Today,
    IReadOnlyList<JourneyItemDto>   Upcoming
);

public sealed record JourneyItemDto(
    int     TopicId,
    int     ContentId,
    string  Slug,
    string  Title,                    // do Content correspondente (lookup no use case)
    string  Reason,                   // string p/ contrato estável (não enum int)
    double  Priority,
    double  EffectiveMastery,
    double  DifficultyScore
);
