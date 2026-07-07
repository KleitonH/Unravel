using Unravel.Application.Journey.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;

namespace Unravel.Application.Journey.Onboarding;

/// <summary>
/// Etapa 1 do onboarding: usuário escolheu trilhas, devolvemos o teste de
/// nivelamento. Estado é puramente derivado (não persistimos a sessão de
/// teste em si — o submit reconstrói o gabarito a partir das mesmas
/// entradas determinísticas, ou seja, mesmo grafo gera mesmas perguntas).
///
/// <para><b>Por que não persistir a sessão</b>: economizar uma tabela e
/// uma migration. Como o LevelingTestBuilder é determinístico (mesmo
/// grafo → mesmos drafts), o submit pode regenerar o gabarito original
/// recomputando — desde que ninguém mexa nos Contents da trilha entre
/// as duas requisições, o que é assumível na janela de minutos do
/// onboarding. Se virar problema (moderador editando durante), basta
/// snapshotar.</para>
/// </summary>
public sealed class StartOnboardingUseCase
{
    private readonly IOnboardingReadModel _readModel;
    private readonly LevelingTestBuilder  _builder;

    public StartOnboardingUseCase(
        IOnboardingReadModel readModel,
        LevelingTestBuilder  builder)
    {
        _readModel = readModel;
        _builder   = builder;
    }

    public async Task<OnboardingTestResponse?> ExecuteAsync(
        Guid userId, OnboardingStartRequest request, CancellationToken ct = default)
    {
        if (request.TrailIds.Count == 0) return null;

        // Idempotência: se o user já tem masteries em qualquer trilha pedida,
        // assumimos que já passou pelo onboarding daquela trilha.
        if (await _readModel.UserHasAnyMasteryAsync(userId, request.TrailIds, ct))
            throw new InvalidOperationException("Onboarding já realizado para alguma das trilhas selecionadas.");

        var trails   = await _readModel.GetTrailsByIdsAsync(request.TrailIds, ct);
        var validIds = trails.Select(t => t.Id).ToHashSet();

        var contentsByTrail   = await _readModel.GetContentsForTrailsAsync(validIds, ct);
        var challengesByTrail = await _readModel.GetLevelingChallengesForTrailsAsync(validIds, ct);

        var groups = new List<LevelingTrailGroup>();
        foreach (var trail in trails)
        {
            var trailChallenges = challengesByTrail.GetValueOrDefault(trail.Id, Array.Empty<GeneratedChallenge>());
            if (trailChallenges.Count == 0) continue;

            var contentsById = contentsByTrail.GetValueOrDefault(trail.Id, Array.Empty<Content>())
                .ToDictionary(c => c.Id);

            var drafts = _builder.Build(trailChallenges, contentsById);
            if (drafts.Count == 0) continue;

            var questions = drafts.Select(d => new LevelingQuestion(
                ChallengeId:      d.ChallengeId,
                TopicId:          d.TopicId,
                ContentId:        d.Content.Id,
                ContentTitle:     d.Content.Title,
                Strategy:         d.Draft.Strategy.ToString(),
                Prompt:           d.Draft.Prompt,
                Options:          d.Draft.Options,
                DifficultyTarget: Math.Round(d.Draft.EstimatedDifficulty, 4))).ToList();

            groups.Add(new LevelingTrailGroup(trail.Id, trail.Name, questions));
        }

        return new OnboardingTestResponse(groups);
    }
}

/// <summary>Leituras agregadas que os use cases do onboarding precisam.
/// Mesma motivação dos outros read models (Application livre de DbContext).</summary>
public interface IOnboardingReadModel
{
    Task<IReadOnlyList<TrailMeta>> GetTrailsByIdsAsync(
        IReadOnlyCollection<int> trailIds, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, IReadOnlyList<Content>>> GetContentsForTrailsAsync(
        IReadOnlyCollection<int> trailIds, CancellationToken ct = default);

    /// <summary>Perguntas do pipeline forte (LlmGrounded/ModeratorAuthored)
    /// já geradas para as trilhas, agrupadas por <b>TrailId</b> e ordenadas
    /// por Id (determinístico). Fonte do teste de nivelamento — reusamos a
    /// alta qualidade do pipeline em vez de gerar template na hora.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<Unravel.Domain.Forge.GeneratedChallenge>>> GetLevelingChallengesForTrailsAsync(
        IReadOnlyCollection<int> trailIds, CancellationToken ct = default);

    Task<bool> UserHasAnyMasteryAsync(
        Guid userId, IReadOnlyCollection<int> trailIds, CancellationToken ct = default);
}

/// <summary>Reuso da projeção definida em <c>IJourneyReadModel</c> — só
/// reexportamos o nome aqui para não criar dependência cruzada de namespace
/// nas portas do onboarding.</summary>
public sealed record TrailMeta(int Id, string Name);
