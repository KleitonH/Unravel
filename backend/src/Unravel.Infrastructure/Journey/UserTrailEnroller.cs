using Microsoft.EntityFrameworkCore;
using Unravel.Application.Journey.Onboarding;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Journey;

/// <summary>Implementação direta de <see cref="IUserTrailEnroller"/>.
/// Idempotente: se o user já está inscrito (registro <c>UserTrail</c>
/// existente), apenas reativa. Não chama <c>ITrailService.EnrollAsync</c>
/// para não puxar o service inteiro como dependência do onboarding.</summary>
public sealed class UserTrailEnroller : IUserTrailEnroller
{
    private readonly ApplicationDbContext _db;
    public UserTrailEnroller(ApplicationDbContext db) => _db = db;

    public async Task EnrollAsync(Guid userId, int trailId, CancellationToken ct = default)
    {
        var existing = await _db.UserTrail
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TrailId == trailId, ct);

        if (existing is null)
            _db.UserTrail.Add(new UserTrail { UserId = userId, TrailId = trailId });
        else
            existing.IsActive = true;

        await _db.SaveChangesAsync(ct);
    }
}
