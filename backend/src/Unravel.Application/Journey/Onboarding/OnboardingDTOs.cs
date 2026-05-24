namespace Unravel.Application.Journey.Onboarding;

/// <summary>Requisição de início — usuário escolhe as trilhas que quer
/// estudar. O sistema devolve um teste curto para inferir o nível inicial
/// em cada uma.</summary>
public sealed record OnboardingStartRequest(IReadOnlyList<int> TrailIds);

/// <summary>Teste de nivelamento agrupado por trilha. Cada
/// <see cref="LevelingQuestion"/> sabe a qual <c>TopicId</c> pertence —
/// o submit precisa disso para inicializar a Mastery correta.</summary>
public sealed record OnboardingTestResponse(
    IReadOnlyList<LevelingTrailGroup> Trails);

public sealed record LevelingTrailGroup(
    int                            TrailId,
    string                         TrailName,
    IReadOnlyList<LevelingQuestion> Questions);

public sealed record LevelingQuestion(
    int                  TopicId,
    int                  ContentId,
    string               ContentTitle,
    string               Strategy,
    string               Prompt,
    IReadOnlyList<string> Options,
    double               DifficultyTarget        // a dificuldade alvo do topic — útil para a UI
);

// ── Submit ───────────────────────────────────────────────────────────

public sealed record OnboardingSubmitRequest(
    IReadOnlyList<LevelingAnswer> Answers);

/// <summary>Resposta a uma das perguntas do teste. O cliente devolve o
/// índice escolhido (o gabarito vive no servidor).</summary>
public sealed record LevelingAnswer(
    int TopicId,
    int SelectedOptionIndex);

public sealed record OnboardingResultResponse(
    IReadOnlyList<TrailLevelEstimate> Estimates,
    IReadOnlyList<int>                EnrolledTrailIds);

public sealed record TrailLevelEstimate(
    int     TrailId,
    string  TrailName,
    double  EstimatedMastery,       // média ponderada do acerto nos topics testados
    string  Label                   // "Iniciante" / "Intermediário" / "Avançado"
);
