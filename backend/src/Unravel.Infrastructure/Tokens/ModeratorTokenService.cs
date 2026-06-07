using Microsoft.EntityFrameworkCore;
using Unravel.Application.Tokens.Ports;
using Unravel.Domain.Tokens;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Tokens;

/// <summary>
/// PR 52 — impl Postgres. Cada Credit/Debit é uma transação dupla
/// (atualiza balance + insere transaction). EF Core SaveChanges
/// envolve ambas em uma TX implícita.
/// </summary>
public sealed class ModeratorTokenService : IModeratorTokenService
{
    public const int WelcomeBonusCm = 1000;

    private readonly ApplicationDbContext _db;

    public ModeratorTokenService(ApplicationDbContext db) => _db = db;

    public async Task<int> GetBalanceAsync(Guid userId, CancellationToken ct = default)
    {
        var bal = await _db.ModeratorTokenBalance
            .FirstOrDefaultAsync(b => b.UserId == userId, ct);
        if (bal is not null) return bal.BalanceCm;

        // Cria saldo zerado pra simplificar callers (idempotente).
        bal = new ModeratorTokenBalance
        {
            UserId    = userId,
            BalanceCm = 0,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.ModeratorTokenBalance.Add(bal);
        await _db.SaveChangesAsync(ct);
        return 0;
    }

    public async Task<int> CreditAsync(
        Guid userId, int amountCm, TokenTransactionReason reason,
        string? metadata = null, CancellationToken ct = default)
    {
        if (amountCm <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountCm), "amountCm must be > 0");

        var bal = await GetOrCreateBalanceAsync(userId, ct);
        bal.BalanceCm += amountCm;
        bal.UpdatedAt  = DateTime.UtcNow;

        _db.ModeratorTokenTransaction.Add(new ModeratorTokenTransaction
        {
            UserId    = userId,
            DeltaCm   = +amountCm,
            Reason    = reason,
            Metadata  = metadata,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return bal.BalanceCm;
    }

    public async Task<int> DebitAsync(
        Guid userId, int amountCm, TokenTransactionReason reason,
        string? metadata = null, CancellationToken ct = default)
    {
        if (amountCm <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountCm), "amountCm must be > 0");

        var bal = await GetOrCreateBalanceAsync(userId, ct);
        if (bal.BalanceCm < amountCm)
            throw new InsufficientTokensException(bal.BalanceCm, amountCm);

        bal.BalanceCm -= amountCm;
        bal.UpdatedAt  = DateTime.UtcNow;

        _db.ModeratorTokenTransaction.Add(new ModeratorTokenTransaction
        {
            UserId    = userId,
            DeltaCm   = -amountCm,
            Reason    = reason,
            Metadata  = metadata,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return bal.BalanceCm;
    }

    public async Task EnsureWelcomeBonusAsync(Guid userId, CancellationToken ct = default)
    {
        // Idempotente: verifica se já existe transaction com WelcomeBonus.
        var hasWelcome = await _db.ModeratorTokenTransaction
            .AnyAsync(t => t.UserId == userId && t.Reason == TokenTransactionReason.WelcomeBonus, ct);
        if (hasWelcome) return;

        await CreditAsync(userId, WelcomeBonusCm, TokenTransactionReason.WelcomeBonus,
            metadata: "{\"note\":\"Bem-vindo, moderador!\"}", ct: ct);
    }

    public Task<IReadOnlyList<ModeratorTokenTransaction>> GetHistoryAsync(
        Guid userId, int take = 30, int skip = 0, CancellationToken ct = default)
        => _db.ModeratorTokenTransaction
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ModeratorTokenTransaction>)t.Result, ct);

    private async Task<ModeratorTokenBalance> GetOrCreateBalanceAsync(
        Guid userId, CancellationToken ct)
    {
        var bal = await _db.ModeratorTokenBalance
            .FirstOrDefaultAsync(b => b.UserId == userId, ct);
        if (bal is not null) return bal;

        bal = new ModeratorTokenBalance
        {
            UserId    = userId,
            BalanceCm = 0,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.ModeratorTokenBalance.Add(bal);
        return bal;
    }
}
