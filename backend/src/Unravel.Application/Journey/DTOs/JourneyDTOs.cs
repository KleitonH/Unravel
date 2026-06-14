namespace Unravel.Application.Journey.DTOs;

/// <summary>Resposta serializada para o frontend. Estrutura espelha
/// <c>JourneyPlan</c> mas só com tipos primitivos / strings — facilita
/// versionar contrato HTTP sem amarrar ao Domain.</summary>
public sealed record JourneyPlanResponse(
    Guid                            UserId,
    int                             TrailId,
    string                          TrailName,
    DateTime                        GeneratedAt,
    int                             MetaDia,         // meta efetiva de hoje (já inclui a penalidade)
    IReadOnlyList<JourneyItemDto>   Today,
    IReadOnlyList<JourneyItemDto>   Upcoming,
    // PR 61 — indicador de meta do dia no dashboard.
    int                             CompletedToday = 0,  // desafios respondidos hoje nesta trilha
    int                             MetaPenalty    = 0    // +N na meta por não ter batido ontem
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
