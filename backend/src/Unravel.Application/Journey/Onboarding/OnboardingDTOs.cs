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
    int                  ChallengeId,            // identidade da pergunta (o submit responde por este id)
    int                  TopicId,
    int                  ContentId,
    string               ContentTitle,
    string               Strategy,
    string               Prompt,
    IReadOnlyList<string> Options,
    double               DifficultyTarget        // dificuldade estimada da pergunta — útil para a UI
);

// ── Submit ───────────────────────────────────────────────────────────

public sealed record OnboardingSubmitRequest(
    IReadOnlyList<LevelingAnswer> Answers);

/// <summary>Resposta a uma das perguntas do teste. Identificada pelo
/// <c>ChallengeId</c> (várias perguntas podem vir do mesmo conteúdo/topic,
/// então a chave é a pergunta, não o topic). O gabarito vive no servidor.</summary>
public sealed record LevelingAnswer(
    int ChallengeId,
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
