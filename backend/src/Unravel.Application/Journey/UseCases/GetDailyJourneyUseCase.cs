using Unravel.Application.Journey.DTOs;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.UseCases;

/// <summary>
/// Orquestra a montagem da jornada do dia: lê o grafo (via cache), as
/// masteries do usuário, o estado dele (vidas/streak) e os títulos dos
/// Contents, depois passa para o <see cref="IJourneyPlanner"/> puro e
/// monta o DTO para o frontend.
///
/// <para>Único ponto que toca leituras agregadas; o planner em si é puro.
/// Mantém o algoritmo testável sem mocks pesados.</para>
/// </summary>
public sealed class GetDailyJourneyUseCase
{
    private readonly IKnowledgeGraphCache _graphCache;
    private readonly IMasteryRepository   _masteryRepo;
    private readonly IJourneyPlanner      _planner;
    private readonly IJourneyReadModel    _readModel;

    public GetDailyJourneyUseCase(
        IKnowledgeGraphCache graphCache,
        IMasteryRepository   masteryRepo,
        IJourneyPlanner      planner,
        IJourneyReadModel    readModel)
    {
        _graphCache  = graphCache;
        _masteryRepo = masteryRepo;
        _planner     = planner;
        _readModel   = readModel;
    }

    public async Task<JourneyPlanResponse?> ExecuteAsync(
        Guid userId, int trailId, DateTime asOf, CancellationToken ct = default)
    {
        var trail = await _readModel.GetTrailMetaAsync(trailId, ct);
        if (trail is null) return null;

        var userState = await _readModel.GetUserStateAsync(userId, ct);
        if (userState is null) return null;

        var graph     = await _graphCache.GetOrBuildAsync(trailId, ct);
        var masteries = await _masteryRepo.GetByTrailAsync(userId, trailId, ct);

        var plan = _planner.Plan(new JourneyPlanInput(
            UserId:         userId,
            Graph:          graph,
            Masteries:      masteries,
            LivesAvailable: userState.Lives,
            StreakDays:     userState.StreakDays,
            AsOf:           asOf));

        var contentIds = plan.Today.Concat(plan.Upcoming)
                                   .Select(i => i.ContentId)
                                   .Distinct()
                                   .ToList();
        var titles = contentIds.Count == 0
            ? new Dictionary<int, string>()
            : (Dictionary<int, string>)await _readModel.GetContentTitlesAsync(contentIds, ct);

        JourneyItemDto Map(JourneyItem i) => new(
            TopicId:          i.TopicId,
            ContentId:        i.ContentId,
            Slug:             i.Slug,
            Title:            titles.GetValueOrDefault(i.ContentId, "(sem título)"),
            Reason:           i.Reason.ToString(),
            Priority:         Math.Round(i.Priority, 4),
            EffectiveMastery: Math.Round(i.EffectiveMastery, 4),
            DifficultyScore:  Math.Round(i.DifficultyScore, 4));

        return new JourneyPlanResponse(
            UserId:      userId,
            TrailId:     trailId,
            TrailName:   trail.Name,
            GeneratedAt: plan.GeneratedAt,
            MetaDia:     plan.MetaDia,
            Today:       plan.Today.Select(Map).ToList(),
            Upcoming:    plan.Upcoming.Select(Map).ToList());
    }
}
