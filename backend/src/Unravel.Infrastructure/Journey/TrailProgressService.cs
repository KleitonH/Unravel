using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Journey.UseCases;
using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Journey;

/// <summary>
/// PR 40 — implementação Postgres do <see cref="ITrailProgressService"/>.
///
/// <para><b>Concorrência</b>: assume request-scoped DbContext. Race
/// condition possível em dois submits paralelos do mesmo user/content
/// (UI desabilita botão durante submit, mas double-click via outro tab é
/// teoricamente possível). Aceitamos a race — pior cenário é
/// <c>ChallengesCompleted</c> sobressair em 1 ou desbloquear próximo
/// numa request sem o flag <c>JustCompleted</c>. Sem corrupção.</para>
/// </summary>
public sealed class TrailProgressService : ITrailProgressService
{
    private readonly ApplicationDbContext        _db;
    private readonly IKnowledgeGraphCache?       _graphCache;
    private readonly IMasteryRepository?         _masteryRepo;
    private readonly IJourneyPlanner?            _planner;
    private readonly IJourneyReadModel?          _readModel;
    private readonly ILogger<TrailProgressService>? _log;

    // Ctor legacy — usado pelos testes existentes que não dependem
    // de recomendações do planner. GetTrailMapAsync ainda funciona,
    // só não marca IsRecommended (todos os nodes ficam false).
    public TrailProgressService(ApplicationDbContext db, ILogger<TrailProgressService>? log = null)
    {
        _db  = db;
        _log = log;
    }

    /// <summary>PR 42b — ctor "rico" usado pelo DI. Recebe as peças do
    /// JourneyPlanner pra enriquecer GetTrailMapAsync com flag IsRecommended
    /// nas ilhas que o planner sugeriria como meta do dia.</summary>
    public TrailProgressService(
        ApplicationDbContext db,
        IKnowledgeGraphCache graphCache,
        IMasteryRepository   masteryRepo,
        IJourneyPlanner      planner,
        IJourneyReadModel    readModel,
        ILogger<TrailProgressService>? log = null)
        : this(db, log)
    {
        _graphCache  = graphCache;
        _masteryRepo = masteryRepo;
        _planner     = planner;
        _readModel   = readModel;
    }

    public async Task<ProgressUpdate> RecordChallengeAsync(
        Guid userId, int contentId, CancellationToken ct = default)
    {
        // Carrega Content (pra ChallengesRequired + TrailId) + UserContent atual.
        var content = await _db.Content
            .Where(c => c.Id == contentId)
            .Select(c => new { c.Id, c.TrailId, c.ChallengesRequired, c.Order })
            .FirstOrDefaultAsync(ct);
        if (content is null)
            throw new InvalidOperationException($"Content {contentId} não existe.");

        var uc = await _db.UserContent.FirstOrDefaultAsync(
            x => x.UserId == userId && x.ContentId == contentId, ct);

        if (uc is null)
        {
            // Aluno tocou o quiz direto via deep-link sem passar pelo enroll
            // (cenário raro mas possível). Criamos UserContent on-the-fly
            // como Available — graceful, evita 404 confuso.
            uc = new UserContent
            {
                UserId      = userId,
                ContentId   = contentId,
                Status      = UserContentStatus.Available,
                StartedAt   = DateTime.UtcNow,
            };
            _db.UserContent.Add(uc);
        }

        var wasAlreadyCompleted = uc.IsCompleted;

        uc.ChallengesCompleted++;
        if (!wasAlreadyCompleted && uc.Status == UserContentStatus.Available)
            uc.Status = UserContentStatus.InProgress;

        int? nextUnlockedId = null;
        var justCompleted = false;

        if (!wasAlreadyCompleted && uc.ChallengesCompleted >= content.ChallengesRequired)
        {
            uc.IsCompleted = true;
            uc.CompletedAt = DateTime.UtcNow;
            uc.Status      = UserContentStatus.Completed;
            justCompleted  = true;

            // Desbloqueia próximo content da trilha (por Order ASC).
            // Se já existe UserContent pra esse próximo (raro mas possível —
            // moderador alterou Order, ou flow assíncrono), não recria.
            var nextContent = await _db.Content
                .Where(c => c.TrailId == content.TrailId
                            && c.IsActive
                            && c.Order > content.Order)
                .OrderBy(c => c.Order).ThenBy(c => c.Id)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);

            if (nextContent is not null)
            {
                var existingNext = await _db.UserContent.AnyAsync(
                    x => x.UserId == userId && x.ContentId == nextContent.Id, ct);
                if (!existingNext)
                {
                    _db.UserContent.Add(new UserContent
                    {
                        UserId    = userId,
                        ContentId = nextContent.Id,
                        Status    = UserContentStatus.Available,
                        StartedAt = DateTime.UtcNow,
                    });
                    nextUnlockedId = nextContent.Id;
                    _log?.LogInformation(
                        "TrailProgress: user={UserId} completed content={ContentId}, unlocked next={NextContentId}",
                        userId, contentId, nextContent.Id);
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ProgressUpdate(
            ContentId:            contentId,
            ChallengesCompleted:  uc.ChallengesCompleted,
            ChallengesRequired:   content.ChallengesRequired,
            JustCompleted:        justCompleted,
            NextContentIdUnlocked: nextUnlockedId);
    }

    public async Task<TrailMap?> GetTrailMapAsync(
        Guid userId, int trailId, CancellationToken ct = default)
    {
        var trail = await _db.Trail
            .Where(t => t.Id == trailId && t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct);
        if (trail is null) return null;

        // 1. Todos os contents ativos da trilha (ordem definida).
        var contents = await _db.Content
            .Where(c => c.TrailId == trailId && c.IsActive)
            .OrderBy(c => c.Order).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id, c.Title, c.Slug, c.Order, c.ChallengesRequired,
            })
            .ToListAsync(ct);

        // 2. UserContents do user nessa trilha (anti-join com contents).
        var contentIds = contents.Select(c => c.Id).ToList();
        var userContents = await _db.UserContent
            .Where(uc => uc.UserId == userId && contentIds.Contains(uc.ContentId))
            .ToDictionaryAsync(uc => uc.ContentId, ct);

        // 3. PR 42b — recomendações do JourneyPlanner pra HOJE. Falha
        //    silenciosa (planner é augmentação opcional do mapa; se der
        //    ruim, mapa continua funcionando linear sem badges).
        var plannerRecommended = await GetRecommendedContentIdsAsync(userId, trailId, ct);

        // 4. Determina quais contents são ACESSÍVEIS hoje pro user
        //    (Status Available ou InProgress). Filtro essencial: ilha
        //    Locked nunca pode receber badge "Hoje" — aluno não pode
        //    sequer clicar nela.
        var accessibleContentIds = userContents
            .Where(kv => kv.Value.Status == UserContentStatus.Available
                      || kv.Value.Status == UserContentStatus.InProgress)
            .Select(kv => kv.Key)
            .ToHashSet();

        // Interseção: planner sugere + aluno pode acessar.
        var effectiveRecommended = plannerRecommended.Intersect(accessibleContentIds).ToHashSet();

        // Fallback: planner usa o KnowledgeGraph com prerequisites inferidos
        // por keywords; em cold-start ou quando o grafo discorda da ordem
        // SMW da trilha, a interseção fica vazia. Nesse caso, marcamos a
        // PRIMEIRA ilha acessível na ordem do mapa — é a "próxima lógica"
        // pra avançar e o aluno nunca fica sem orientação.
        if (effectiveRecommended.Count == 0 && accessibleContentIds.Count > 0)
        {
            var firstAccessible = contents
                .Where(c => accessibleContentIds.Contains(c.Id))
                .OrderBy(c => c.Order).ThenBy(c => c.Id)
                .Select(c => c.Id)
                .First();
            effectiveRecommended.Add(firstAccessible);
        }

        // 5. Monta nodes. Ausência de UserContent = Locked.
        var nodes = contents.Select(c =>
        {
            var isRec = effectiveRecommended.Contains(c.Id);
            if (userContents.TryGetValue(c.Id, out var uc))
            {
                return new TrailMapNode(
                    ContentId:           c.Id,
                    Title:               c.Title,
                    Slug:                c.Slug,
                    Order:               c.Order,
                    ChallengesRequired:  c.ChallengesRequired,
                    ChallengesCompleted: Math.Min(uc.ChallengesCompleted, c.ChallengesRequired),
                    Status:              uc.Status.ToString(),
                    IsRecommended:       isRec);
            }
            return new TrailMapNode(
                ContentId:           c.Id,
                Title:               c.Title,
                Slug:                c.Slug,
                Order:               c.Order,
                ChallengesRequired:  c.ChallengesRequired,
                ChallengesCompleted: 0,
                Status:              nameof(UserContentStatus.Locked),
                IsRecommended:       false);
        }).ToList();

        return new TrailMap(trail.Id, trail.Name, nodes);
    }

    /// <summary>
    /// PR 42b — invoca o <see cref="IJourneyPlanner"/> pra obter os
    /// content IDs sugeridos como meta do DIA pro user na trilha.
    /// Retorna conjunto vazio se: ctor legacy (sem peças do planner),
    /// trilha sem grafo construído, user sem masteries, ou qualquer
    /// erro (planner é augmentação opcional, falha não derruba o mapa).
    ///
    /// <para>Usa apenas <c>plan.Today</c> — <c>Upcoming</c> são pra
    /// dias futuros, recomendar agora confunde o aluno.</para>
    /// </summary>
    private async Task<HashSet<int>> GetRecommendedContentIdsAsync(
        Guid userId, int trailId, CancellationToken ct)
    {
        if (_graphCache is null || _masteryRepo is null || _planner is null || _readModel is null)
            return new HashSet<int>();

        try
        {
            var userState = await _readModel.GetUserStateAsync(userId, ct);
            if (userState is null) return new HashSet<int>();

            var graph     = await _graphCache.GetOrBuildAsync(trailId, ct);
            if (graph.Topics.Count == 0) return new HashSet<int>();

            var masteries = await _masteryRepo.GetByTrailAsync(userId, trailId, ct);

            var plan = _planner.Plan(new JourneyPlanInput(
                UserId:         userId,
                Graph:          graph,
                Masteries:      masteries,
                LivesAvailable: userState.Lives,
                StreakDays:     userState.StreakDays,
                AsOf:           DateTime.UtcNow));

            return plan.Today.Select(i => i.ContentId).ToHashSet();
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex,
                "JourneyPlanner falhou pro mapa (user={UserId} trail={TrailId}); mapa segue sem recomendações.",
                userId, trailId);
            return new HashSet<int>();
        }
    }

    public async Task<TrailMasteryReport?> GetTrailMasteryAsync(
        Guid userId, int trailId, CancellationToken ct = default)
    {
        var trail = await _db.Trail
            .Where(t => t.Id == trailId && t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct);
        if (trail is null) return null;

        // Sem o ctor "rico" não conseguimos calcular effective (precisa do
        // KnowledgeGraph pra mapear topic→content e do mastery repo).
        if (_graphCache is null || _masteryRepo is null)
            return new TrailMasteryReport(trail.Id, trail.Name, 0, 0, 0, 0, Array.Empty<TopicMasteryItem>());

        var now       = DateTime.UtcNow;
        var graph     = await _graphCache.GetOrBuildAsync(trailId, ct);
        var masteries = await _masteryRepo.GetByTrailAsync(userId, trailId, ct);
        var masteryByTopic = masteries.ToDictionary(m => m.TopicId);

        // Map contentId → metadados (Title, Order) pra cada topic do grafo
        var contentIds = graph.Topics.Select(t => t.ContentId).Where(id => id > 0).Distinct().ToList();
        var contentMeta = await _db.Content
            .Where(c => contentIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Title, c.Order })
            .ToDictionaryAsync(c => c.Id, ct);

        var items = graph.Topics
            .Where(t => t.ContentId > 0 && contentMeta.ContainsKey(t.ContentId))
            .Select(t =>
            {
                var meta = contentMeta[t.ContentId];
                var has  = masteryByTopic.TryGetValue(t.Id, out var m);
                var eff  = has ? MasteryScoring.EffectiveScore(m!, now) : 0.0;
                var due  = has && MasteryScoring.IsDueForReview(m!, now);
                // Severity:
                // - Weak: effective < 0.6  (incl. untouched)
                // - Stale: effective ≥ 0.6 mas SRS due  (decay + tempo de revisão)
                // - Solid: effective ≥ 0.6 e não-due
                var severity = eff < 0.6 ? "Weak"
                            : due        ? "Stale"
                            :              "Solid";
                return new TopicMasteryItem(
                    TopicId:        t.Id,
                    TopicSlug:      t.Slug,
                    ContentId:      t.ContentId,
                    ContentTitle:   meta.Title,
                    Order:          meta.Order,
                    HasMastery:     has,
                    RawScore:       has ? Math.Round(m!.Score, 4) : 0,
                    EffectiveScore: Math.Round(eff, 4),
                    Confidence:     has ? m!.Confidence : 0,
                    LastSeenAt:     has ? m!.LastSeenAt : null,
                    NextDueAt:      has ? m!.NextDueAt  : null,
                    IsSrsDue:       due,
                    Severity:       severity);
            })
            // Ordenação primária: severity (Weak → Stale → Solid), depois
            // por score crescente dentro de cada bucket (mais fraco primeiro).
            .OrderBy(i => i.Severity == "Weak" ? 0 : i.Severity == "Stale" ? 1 : 2)
            .ThenBy(i => i.EffectiveScore)
            .ThenBy(i => i.Order)
            .ToList();

        var touched = items.Where(i => i.HasMastery).ToList();
        var avg     = touched.Count == 0 ? 0 : touched.Average(i => i.EffectiveScore);

        return new TrailMasteryReport(
            TrailId:               trail.Id,
            TrailName:             trail.Name,
            AverageEffectiveScore: Math.Round(avg, 4),
            WeakCount:             items.Count(i => i.Severity == "Weak"),
            SrsDueCount:           items.Count(i => i.IsSrsDue),
            UntouchedCount:        items.Count(i => !i.HasMastery),
            Topics:                items);
    }

    public async Task BootstrapAccessAsync(Guid userId, int trailId, CancellationToken ct = default)
    {
        // Idempotente: se já tem qualquer UserContent nessa trilha, nada a fazer.
        var alreadyHasAny = await _db.UserContent
            .AnyAsync(uc => uc.UserId == userId && uc.Content.TrailId == trailId, ct);
        if (alreadyHasAny) return;

        var firstContent = await _db.Content
            .Where(c => c.TrailId == trailId && c.IsActive)
            .OrderBy(c => c.Order).ThenBy(c => c.Id)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);
        if (firstContent is null) return;

        _db.UserContent.Add(new UserContent
        {
            UserId    = userId,
            ContentId = firstContent.Id,
            Status    = UserContentStatus.Available,
            StartedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }
}
