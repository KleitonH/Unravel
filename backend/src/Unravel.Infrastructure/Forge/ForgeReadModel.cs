using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge;

public sealed class ForgeReadModel : IForgeReadModel
{
    private readonly ApplicationDbContext _db;
    public ForgeReadModel(ApplicationDbContext db) => _db = db;

    public Task<Content?> GetContentAsync(int contentId, CancellationToken ct = default)
        => _db.Content
              .AsNoTracking()
              .FirstOrDefaultAsync(c => c.Id == contentId && c.IsActive, ct);
}
