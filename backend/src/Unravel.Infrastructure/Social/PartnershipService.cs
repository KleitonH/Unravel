using Microsoft.EntityFrameworkCore;
using Unravel.Application.Notifications.Ports;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Social;

/// <summary>
/// Ideia 1 / UC17 — mecânica de Parceria ("Novelo de Lã / Parceiro de Trilha").
/// Acessa o DbContext direto (mesmo padrão de FriendshipService). O par é
/// simétrico (A↔B). Pré-condição: os dois já são amigos.
/// </summary>
public class PartnershipService(ApplicationDbContext db, INotificationService notifications) : IPartnershipService
{
    private const int MaxActivePerUser = 3;   // doc: até 3 parceiros ativos
    private const int NoveloReward     = 100; // moedas pra cada um ao fechar um novelo

    public async Task<PartnershipActionResult> RequestAsync(Guid requesterId, Guid addresseeId, CancellationToken ct = default)
    {
        if (requesterId == addresseeId)
            return new(PartnershipOutcome.CannotSelf);

        var addressee = await db.User.FirstOrDefaultAsync(u => u.Id == addresseeId && u.IsActive, ct);
        if (addressee is null) return new(PartnershipOutcome.NotFound);

        // Pré-condição: amizade aceita entre os dois (Ideia 1).
        var areFriends = await db.Friendship.AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == requesterId && f.AddresseeId == addresseeId) ||
             (f.RequesterId == addresseeId && f.AddresseeId == requesterId)), ct);
        if (!areFriends) return new(PartnershipOutcome.NotFriends);

        // Já existe parceria pendente/ativa entre o par?
        var existing = await db.Partnership.FirstOrDefaultAsync(p =>
            (p.Status == PartnershipStatus.Pending || p.Status == PartnershipStatus.Active) &&
            ((p.RequesterId == requesterId && p.AddresseeId == addresseeId) ||
             (p.RequesterId == addresseeId && p.AddresseeId == requesterId)), ct);
        if (existing is not null) return new(PartnershipOutcome.AlreadyExists, existing.Id);

        if (await ActiveCountAsync(requesterId, ct) >= MaxActivePerUser)
            return new(PartnershipOutcome.LimitReached);

        var partnership = new Partnership
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status      = PartnershipStatus.Pending,
            CreatedAt   = DateTime.UtcNow,
        };
        db.Partnership.Add(partnership);
        db.PartnershipLog.Add(new PartnershipLog
        {
            Partnership = partnership, UserId = requesterId,
            Type = PartnershipEventType.Requested, Message = "Convite de parceria enviado.",
        });
        await db.SaveChangesAsync(ct);

        var requesterName = await NameAsync(requesterId, ct);
        await NotifySafe(addresseeId, NotificationType.PartnershipRequest, "Convite de parceria 🧶",
            $"{requesterName} quer ser seu Parceiro de Trilha.", "/parcerias", ct);

        return new(PartnershipOutcome.Ok, partnership.Id);
    }

    public async Task<PartnershipActionResult> RespondAsync(Guid userId, int partnershipId, bool accept, CancellationToken ct = default)
    {
        var p = await db.Partnership.FirstOrDefaultAsync(x => x.Id == partnershipId, ct);
        if (p is null) return new(PartnershipOutcome.NotFound);

        // Só o destinatário responde, e só enquanto pendente.
        if (p.AddresseeId != userId || p.Status != PartnershipStatus.Pending)
            return new(PartnershipOutcome.NotAuthorized);

        if (!accept)
        {
            p.Status = PartnershipStatus.Declined;
            db.PartnershipLog.Add(new PartnershipLog
            {
                PartnershipId = p.Id, UserId = userId,
                Type = PartnershipEventType.Broken, Message = "Convite recusado.",
            });
            await db.SaveChangesAsync(ct);
            return new(PartnershipOutcome.Ok, p.Id);
        }

        // Teto pode ter sido atingido entre o convite e o aceite.
        if (await ActiveCountAsync(userId, ct) >= MaxActivePerUser)
            return new(PartnershipOutcome.LimitReached);

        var now = DateTime.UtcNow;
        p.Status     = PartnershipStatus.Active;
        p.AcceptedAt = now;

        // Primeiro novelo: começa com quem convidou.
        db.YarnBall.Add(new YarnBall
        {
            Partnership = p, CurrentOwnerId = p.RequesterId,
            CreatedAt = now, UpdatedAt = now, State = YarnState.Ok,
        });
        db.PartnershipLog.Add(new PartnershipLog
        {
            PartnershipId = p.Id, UserId = userId,
            Type = PartnershipEventType.Accepted, Message = "Parceria iniciada! O novelo começou a rolar.",
        });
        await db.SaveChangesAsync(ct);

        var responderName = await NameAsync(userId, ct);
        await NotifySafe(p.RequesterId, NotificationType.PartnershipAccepted, "Parceria aceita! 🧶",
            $"{responderName} topou ser seu Parceiro de Trilha. O novelo está com você.", "/parcerias", ct);

        return new(PartnershipOutcome.Ok, p.Id);
    }

    public async Task<PartnershipActionResult> BreakAsync(Guid userId, int partnershipId, CancellationToken ct = default)
    {
        var p = await db.Partnership.FirstOrDefaultAsync(x => x.Id == partnershipId, ct);
        if (p is null) return new(PartnershipOutcome.NotFound);
        if (p.RequesterId != userId && p.AddresseeId != userId)
            return new(PartnershipOutcome.NotAuthorized);

        p.Status = PartnershipStatus.Broken;
        // Descontinua o novelo ativo.
        var yarn = await db.YarnBall.Where(y => y.PartnershipId == p.Id && !y.IsCompleted).ToListAsync(ct);
        foreach (var y in yarn) { y.IsCompleted = true; y.State = YarnState.Dropped; y.UpdatedAt = DateTime.UtcNow; }
        db.PartnershipLog.Add(new PartnershipLog
        {
            PartnershipId = p.Id, UserId = userId,
            Type = PartnershipEventType.Broken, Message = "Parceria desfeita.",
        });
        await db.SaveChangesAsync(ct);
        return new(PartnershipOutcome.Ok, p.Id);
    }

    public async Task<IReadOnlyList<PartnershipDto>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var partnerships = await db.Partnership.AsNoTracking()
            .Where(p => p.Status == PartnershipStatus.Active &&
                        (p.RequesterId == userId || p.AddresseeId == userId))
            .Select(p => new
            {
                p.Id, p.NovelosCompleted, p.RequesterId, p.AddresseeId,
                Partner = p.RequesterId == userId ? p.Addressee! : p.Requester!,
                Yarn = db.YarnBall.Where(y => y.PartnershipId == p.Id && !y.IsCompleted)
                                  .OrderByDescending(y => y.Id).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return partnerships.Select(x => new PartnershipDto(
            x.Id, x.Partner.Id, x.Partner.Name, x.Partner.Xp, x.Partner.ActiveTitle,
            PartnershipStatus.Active.ToString(), x.NovelosCompleted,
            x.Yarn is null ? null : new YarnBallDto(
                x.Yarn.Id, x.Yarn.CurrentOwnerId, x.Yarn.CurrentOwnerId == userId,
                x.Yarn.Progress, x.Yarn.DailyGoal, x.Yarn.CyclesCompleted, x.Yarn.TotalCycles,
                x.Yarn.State.ToString())))
            .OrderByDescending(d => d.NovelosCompleted)
            .ToList();
    }

    public async Task<PartnershipRequestsDto> GetRequestsAsync(Guid userId, CancellationToken ct = default)
    {
        var pending = await db.Partnership.AsNoTracking()
            .Where(p => p.Status == PartnershipStatus.Pending &&
                        (p.AddresseeId == userId || p.RequesterId == userId))
            .Select(p => new
            {
                p.Id, p.CreatedAt,
                Incoming  = p.AddresseeId == userId,
                Partner   = p.AddresseeId == userId ? p.Requester! : p.Addressee!,
            })
            .ToListAsync(ct);

        var mapped = pending.Select(p => new PartnershipRequestDto(
            p.Id, p.Partner.Id, p.Partner.Name, p.Partner.Xp,
            p.Incoming ? "incoming" : "outgoing",
            p.CreatedAt.ToString("dd/MM/yyyy"))).ToList();

        return new PartnershipRequestsDto(
            mapped.Where(m => m.Direction == "incoming").ToList(),
            mapped.Where(m => m.Direction == "outgoing").ToList());
    }

    public async Task<AddProgressResult> AddProgressAsync(Guid userId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0) return new(0, false, false);
        var now = DateTime.UtcNow;

        // Novelos ativos em que o usuário é o dono atual.
        var balls = await db.YarnBall
            .Where(y => !y.IsCompleted && y.CurrentOwnerId == userId &&
                        y.Partnership!.Status == PartnershipStatus.Active)
            .Include(y => y.Partnership)
            .ToListAsync(ct);
        if (balls.Count == 0) return new(0, false, false);

        var anyPassed = false; var anyCompleted = false;

        foreach (var y in balls)
        {
            y.Progress += amount;
            y.LastProgressAt = now;
            y.UpdatedAt = now;
            if (y.State != YarnState.Dropped) y.State = YarnState.Ok; // recupera de "enrolado"

            // Desenrola quantos ciclos couberem.
            while (!y.IsCompleted && y.Progress >= y.DailyGoal)
            {
                y.Progress -= y.DailyGoal;
                y.CyclesCompleted++;

                if (y.CyclesCompleted >= y.TotalCycles)
                {
                    await CompleteNoveloAsync(y, now, ct);
                    anyCompleted = true;
                }
                else
                {
                    // Passa o novelo pro parceiro.
                    var partner = OtherUser(y.Partnership!, userId);
                    y.CurrentOwnerId = partner;
                    db.PartnershipLog.Add(new PartnershipLog
                    {
                        PartnershipId = y.PartnershipId, UserId = userId,
                        Type = PartnershipEventType.Passed,
                        Message = $"Meta cumprida! Novelo passou (ciclo {y.CyclesCompleted}/{y.TotalCycles}).",
                    });
                    anyPassed = true;
                    var name = await NameAsync(userId, ct);
                    await NotifySafe(partner, NotificationType.YarnPassed, "O novelo é seu! 🧶",
                        $"{name} cumpriu a meta e te passou o novelo. Sua vez!", "/parcerias", ct);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new(balls.Count, anyPassed, anyCompleted);
    }

    public async Task<int> EvaluateInactivityAsync(DateTime now, CancellationToken ct = default)
    {
        var balls = await db.YarnBall
            .Where(y => !y.IsCompleted && y.Partnership!.Status == PartnershipStatus.Active)
            .Include(y => y.Partnership)
            .ToListAsync(ct);

        var affected = 0;
        foreach (var y in balls)
        {
            var since = now - (y.LastProgressAt ?? y.CreatedAt);
            if (since.TotalDays >= 2 && y.State != YarnState.Dropped)
            {
                // Falha grave: novelo cai, parceria reinicia com um novelo novo.
                y.IsCompleted = true; y.State = YarnState.Dropped; y.UpdatedAt = now;
                db.YarnBall.Add(new YarnBall
                {
                    PartnershipId = y.PartnershipId, CurrentOwnerId = y.CurrentOwnerId,
                    CreatedAt = now, UpdatedAt = now, State = YarnState.Ok,
                });
                db.PartnershipLog.Add(new PartnershipLog
                {
                    PartnershipId = y.PartnershipId, UserId = y.CurrentOwnerId,
                    Type = PartnershipEventType.Dropped, Message = "O novelo caiu 😿 — recomeçando.",
                });
                affected++;
            }
            else if (since.TotalDays >= 1 && y.State == YarnState.Ok)
            {
                y.State = YarnState.Tangled; y.UpdatedAt = now;
                db.PartnershipLog.Add(new PartnershipLog
                {
                    PartnershipId = y.PartnershipId, UserId = y.CurrentOwnerId,
                    Type = PartnershipEventType.Tangled, Message = "Novelo enrolado — cumpra a meta hoje!",
                });
                affected++;
            }
        }

        if (affected > 0) await db.SaveChangesAsync(ct);
        return affected;
    }

    // ---- helpers ----

    private async Task CompleteNoveloAsync(YarnBall y, DateTime now, CancellationToken ct)
    {
        y.IsCompleted = true; y.State = YarnState.Ok; y.UpdatedAt = now;
        var p = y.Partnership!;
        p.NovelosCompleted++;

        // Recompensa: moedas pra ambos. (Cosméticos raros ficam pra fase de FE.)
        var users = await db.User.Where(u => u.Id == p.RequesterId || u.Id == p.AddresseeId).ToListAsync(ct);
        foreach (var u in users) u.Coins += NoveloReward;

        db.PartnershipLog.Add(new PartnershipLog
        {
            PartnershipId = p.Id, UserId = y.CurrentOwnerId,
            Type = PartnershipEventType.NoveloCompleted,
            Message = $"Novelo nº {p.NovelosCompleted} concluído! +{NoveloReward} moedas pra dupla 🎉",
        });

        // Próximo novelo recomeça com quem convidou.
        db.YarnBall.Add(new YarnBall
        {
            PartnershipId = p.Id, CurrentOwnerId = p.RequesterId,
            CreatedAt = now, UpdatedAt = now, State = YarnState.Ok,
        });

        await NotifySafe(p.RequesterId, NotificationType.YarnPassed, "Novelo concluído! 🎉",
            $"Vocês fecharam o novelo nº {p.NovelosCompleted}. +{NoveloReward} moedas!", "/parcerias", ct);
        await NotifySafe(p.AddresseeId, NotificationType.YarnPassed, "Novelo concluído! 🎉",
            $"Vocês fecharam o novelo nº {p.NovelosCompleted}. +{NoveloReward} moedas!", "/parcerias", ct);
    }

    private static Guid OtherUser(Partnership p, Guid userId)
        => p.RequesterId == userId ? p.AddresseeId : p.RequesterId;

    private Task<int> ActiveCountAsync(Guid userId, CancellationToken ct)
        => db.Partnership.CountAsync(p => p.Status == PartnershipStatus.Active &&
                                          (p.RequesterId == userId || p.AddresseeId == userId), ct);

    private Task<string?> NameAsync(Guid userId, CancellationToken ct)
        => db.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);

    private async Task NotifySafe(Guid userId, NotificationType type, string title, string body, string? link, CancellationToken ct)
    {
        try { await notifications.CreateAsync(userId, type, title, body, link, ct); }
        catch { /* best-effort */ }
    }
}
