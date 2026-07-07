using Unravel.Application.Journey.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;   // MasteryScoring

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
    private readonly IOnboardingReadModel _readModel;
    private readonly LevelingTestBuilder  _builder;
    private readonly IMasteryRepository   _masteryRepo;
    private readonly IUserTrailEnroller   _enroller;

    public SubmitOnboardingUseCase(
        IOnboardingReadModel readModel,
        LevelingTestBuilder  builder,
        IMasteryRepository   masteryRepo,
        IUserTrailEnroller   enroller)
    {
        _readModel   = readModel;
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

        var answersByChallenge = request.Answers.ToDictionary(a => a.ChallengeId, a => a.SelectedOptionIndex);

        var trails            = await _readModel.GetTrailsByIdsAsync(trailIds, ct);
        var validIds          = trails.Select(t => t.Id).ToList();
        var contentsByTrail   = await _readModel.GetContentsForTrailsAsync(validIds, ct);
        var challengesByTrail = await _readModel.GetLevelingChallengesForTrailsAsync(validIds, ct);

        var allMasteries = new List<Mastery>();
        var estimates    = new List<TrailLevelEstimate>();
        var enrolledIds  = new List<int>();
        var now          = DateTime.UtcNow;

        foreach (var trail in trails)
        {
            var trailChallenges = challengesByTrail.GetValueOrDefault(trail.Id, Array.Empty<GeneratedChallenge>());
            if (trailChallenges.Count == 0) continue;

            var contentsById = contentsByTrail.GetValueOrDefault(trail.Id, Array.Empty<Content>())
                .ToDictionary(c => c.Id);

            var drafts = _builder.Build(trailChallenges, contentsById);
            if (drafts.Count == 0) continue;

            // Uma resposta por pergunta (ChallengeId). Guarda por topic pra
            // (a) semear a Mastery e (b) estimar o nível ponderando por dificuldade.
            var outcomesForTrail = new List<(double Outcome, double Difficulty)>();
            var outcomesByTopic  = new Dictionary<int, List<double>>();
            foreach (var d in drafts)
            {
                if (!answersByChallenge.TryGetValue(d.ChallengeId, out var selected)) continue;
                var outcome = selected == d.Draft.CorrectIndex ? 1.0 : 0.0;
                outcomesForTrail.Add((outcome, d.Draft.EstimatedDifficulty));
                (outcomesByTopic.TryGetValue(d.TopicId, out var l) ? l : outcomesByTopic[d.TopicId] = new()).Add(outcome);
            }

            if (outcomesForTrail.Count == 0) continue;

            // Semeia UMA Mastery por topic com a média dos acertos naquele topic
            // (várias perguntas podem cair no mesmo conteúdo/topic).
            foreach (var (topicId, outs) in outcomesByTopic)
            {
                var avg     = outs.Average();
                var initial = Mastery.Initial(userId, topicId, trail.Id, now);
                var updated = MasteryScoring.Apply(initial, avg, now);
                allMasteries.Add(updated);
            }

            // Estimativa do nível: média ponderada pela dificuldade das perguntas
            // (acertar pergunta difícil pesa mais que acertar pergunta fácil).
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
