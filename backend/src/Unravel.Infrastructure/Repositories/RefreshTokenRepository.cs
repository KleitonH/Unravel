using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.Ports;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Repositories;

public class RefreshTokenRepository(ApplicationDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, cancellationToken);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        context.RefreshTokens.Update(refreshToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
