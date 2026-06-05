using Unravel.Application.Journey.Ports;

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
    double               EstimatedDifficulty,
    int                  ContentId = 0         // PR 37: quiz normal já sabe o contentId pelo wrapper, mas o reinforcement quiz mistura contents — precisa explícito por challenge pra rotear o submit
);

// ── Submit (PR 13) ─────────────────────────────────────────────────────

/// <summary>Resposta enviada pelo cliente para validar e propagar o sinal
/// até a Mastery. ID veio do <see cref="PoolChallengeDto.Id"/>.</summary>
public sealed record SubmitPoolChallengeRequest(
    int GeneratedChallengeId,
    int SelectedOptionIndex
);

/// <summary>Resultado autoritativo: gabarito vem do servidor (cliente
/// nunca decide se acertou), explicação, atualização visível da mastery
/// do tópico e <b>ganhos de gamificação</b> (XP/Coins/Stars/Vidas/Streak)
/// para a UI mostrar feedback rico no quiz.</summary>
public sealed record SubmitPoolChallengeResponse(
    bool             IsCorrect,
    int              CorrectOptionIndex,
    string?          Explanation,
    double           NewMasteryScore,
    int              NewMasteryConfidence,
    int              XpEarned,
    int              CoinsEarned,
    int              StarsEarned,
    int              LifeDelta,        // -1 em erro, 0 em acerto
    int              TotalXp,
    int              TotalCoins,
    int              TotalStars,
    int              TotalLives,
    int              StreakDays,
    ProgressUpdate?  Progress = null   // PR 40 — null se service não foi injetado (testes legacy)
);
