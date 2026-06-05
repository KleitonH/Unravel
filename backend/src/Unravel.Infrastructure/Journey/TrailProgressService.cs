using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Entities;
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
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TrailProgressService>? _log;

    public TrailProgressService(ApplicationDbContext db, ILogger<TrailProgressService>? log = null)
    {
        _db  = db;
        _log = log;
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

        // 3. Monta nodes. Ausência de UserContent = Locked.
        var nodes = contents.Select(c =>
        {
            if (userContents.TryGetValue(c.Id, out var uc))
            {
                return new TrailMapNode(
                    ContentId:           c.Id,
                    Title:               c.Title,
                    Slug:                c.Slug,
                    Order:               c.Order,
                    ChallengesRequired:  c.ChallengesRequired,
                    ChallengesCompleted: Math.Min(uc.ChallengesCompleted, c.ChallengesRequired),
                    Status:              uc.Status.ToString());
            }
            return new TrailMapNode(
                ContentId:           c.Id,
                Title:               c.Title,
                Slug:                c.Slug,
                Order:               c.Order,
                ChallengesRequired:  c.ChallengesRequired,
                ChallengesCompleted: 0,
                Status:              nameof(UserContentStatus.Locked));
        }).ToList();

        return new TrailMap(trail.Id, trail.Name, nodes);
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
