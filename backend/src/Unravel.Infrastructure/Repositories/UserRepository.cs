using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.Ports;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Repositories;

public class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email.Value == email.Value, cancellationToken);

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        await context.Users.AnyAsync(u => u.Email.Value == email.Value, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
