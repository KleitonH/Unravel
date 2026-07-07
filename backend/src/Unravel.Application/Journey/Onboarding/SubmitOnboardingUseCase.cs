using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Onboarding;

/// <summary>
/// Etapa 2 do onboarding: recebe as respostas, recomputa o gabarito a partir
/// do grafo (determinístico → mesmas perguntas → mesmas alternativas) e
/// inicializa <see cref="Mastery"/> dos topics testados. Inscreve o usuário
/// nas trilhas selecionadas.
///
/// <para><b>Por que recomputar o gabarito</b>: ver <see cref="StartOnboardingUseCase"/> —
/// economia de uma tabela "OnboardingSession" sem perda real, dado que o
/// Forge é determinístico e a janela é curta.</para>
/// </summary>
public sealed class SubmitOnboardingUseCase
{
    private readonly IOnboardingReadModel    _readModel;
    private readonly IKnowledgeGraphCache    _graphCache;
    private readonly LevelingTestBuilder     _builder;
    private readonly IMasteryRepository      _masteryRepo;
    private readonly IUserTrailEnroller      _enroller;

    public SubmitOnboardingUseCase(
        IOnboardingReadModel readModel,
        IKnowledgeGraphCache graphCache,
        LevelingTestBuilder  builder,
        IMasteryRepository   masteryRepo,
        IUserTrailEnroller   enroller)
    {
        _readModel   = readModel;
        _graphCache  = graphCache;
        _builder     = builder;
        _masteryRepo = masteryRepo;
        _enroller    = enroller;
    }

    public async Task<OnboardingResultResponse> ExecuteAsync(
        Guid userId,
        IReadOnlyList<int> trailIds,
        OnboardingSubmitRequest request,
        CancellationToken ct = default)
    {
        if (trailIds.Count == 0)
            throw new ArgumentException("trailIds vazio.", nameof(trailIds));

        var answersByTopic = request.Answers.ToDictionary(a => a.TopicId, a => a.SelectedOptionIndex);

        var trails              = await _readModel.GetTrailsByIdsAsync(trailIds, ct);
        var validIds            = trails.Select(t => t.Id).ToList();
        var contentsByTrail     = await _readModel.GetContentsForTrailsAsync(validIds, ct);
        var challengesByContent = await _readModel.GetLevelingChallengesForTrailsAsync(validIds, ct);

        var allMasteries = new List<Mastery>();
        var estimates    = new List<TrailLevelEstimate>();
        var enrolledIds  = new List<int>();
        var now          = DateTime.UtcNow;

        foreach (var trail in trails)
        {
            var graph = await _graphCache.GetOrBuildAsync(trail.Id, ct);
            if (graph.Topics.Count == 0) continue;

            var contents = contentsByTrail.GetValueOrDefault(trail.Id, Array.Empty<Content>())
                .ToDictionary(c => c.Id);

            var drafts = _builder.Build(graph, contents, challengesByContent);
            if (drafts.Count == 0) continue;

            var outcomesForTrail = new List<(int TopicId, double Outcome, double Difficulty)>();
            foreach (var d in drafts)
            {
                if (!answersByTopic.TryGetValue(d.Topic.Id, out var selected)) continue;
                var correct = selected == d.Draft.CorrectIndex;
                outcomesForTrail.Add((d.Topic.Id, correct ? 1.0 : 0.0, d.Topic.DifficultyScore));

                // Inicializa Mastery do topic com 1 tentativa registrada.
                var initial = Mastery.Initial(userId, d.Topic.Id, trail.Id, now);
                var updated = MasteryScoring.Apply(initial, correct ? 1.0 : 0.0, now);
                allMasteries.Add(updated);
            }

            if (outcomesForTrail.Count == 0) continue;

            // Estimativa do nível: média ponderada pela dificuldade dos topics
            // testados (acertar topic difícil pesa mais que acertar topic fácil).
            var weightSum = outcomesForTrail.Sum(x => x.Difficulty);
            var weighted  = weightSum > 0
                ? outcomesForTrail.Sum(x => x.Outcome * x.Difficulty) / weightSum
                : outcomesForTrail.Average(x => x.Outcome);

            estimates.Add(new TrailLevelEstimate(
                TrailId:          trail.Id,
                TrailName:        trail.Name,
                EstimatedMastery: Math.Round(weighted, 4),
                Label:            LabelFor(weighted)));

            await _enroller.EnrollAsync(userId, trail.Id, ct);
            enrolledIds.Add(trail.Id);
        }

        if (allMasteries.Count > 0)
            await _masteryRepo.UpsertManyAsync(allMasteries, ct);

        return new OnboardingResultResponse(estimates, enrolledIds);
    }

    private static string LabelFor(double mastery) => mastery switch
    {
        < 0.35 => "Iniciante",
        < 0.70 => "Intermediário",
        _      => "Avançado",
    };
}

/// <summary>Port mínima para inscrever um usuário numa trilha. Existe para
/// o use case não depender do <c>ITrailService</c> inteiro (que carrega
/// 8 métodos não relacionados a onboarding).</summary>
public interface IUserTrailEnroller
{
    Task EnrollAsync(Guid userId, int trailId, CancellationToken ct = default);
}
