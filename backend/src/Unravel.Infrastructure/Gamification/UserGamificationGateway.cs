using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Gamification.Ports;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Gamification;

/// <summary>
/// Implementação EF de <see cref="IUserGamificationGateway"/>. Carrega o
/// usuário rastreado, aplica deltas, dispara <see cref="StreakUpdater"/>
/// e persiste numa única transação. Lives capadas em [0, MaxLives].
///
/// <para><b>MaxLives</b> = 10 espelha o cap usado em
/// <c>ChallengeService.ProcessDailyLoginAsync</c> (já existente). Centralizar
/// como constante aqui evita drift entre fluxos.</para>
///
/// <para>PR 67 — após aplicar as recompensas individuais, credita o XP ganho
/// na meta coletiva diária da caixinha do usuário (best-effort: uma falha aqui
/// não pode derrubar o submit do quiz).</para>
/// </summary>
public sealed class UserGamificationGateway : IUserGamificationGateway
{
    private readonly ApplicationDbContext _db;
    private readonly ICaixinhaContributionService _caixinha;
    private readonly ILogger<UserGamificationGateway> _logger;
    public const int MaxLives = 10;

    public UserGamificationGateway(
        ApplicationDbContext db,
        ICaixinhaContributionService caixinha,
        ILogger<UserGamificationGateway> logger)
    {
        _db = db;
        _caixinha = caixinha;
        _logger = logger;
    }

    public async Task<UserGamificationSnapshot> ApplyAsync(
        Guid userId, SubmissionRewards rewards, DateTime asOfUtc, CancellationToken ct = default)
    {
        var user = await _db.User.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new InvalidOperationException(
                       $"User {userId} não encontrado ao aplicar recompensas.");

        user.Xp    += rewards.Xp;
        user.Coins += rewards.Coins;
        user.Stars += rewards.Stars;
        user.Lives  = Math.Clamp(user.Lives + rewards.LifeDelta, 0, MaxLives);

        // Cada submit conta como atividade — o doc Ofensiva trata engajamento
        // independente de acerto/erro.
        StreakUpdater.RegisterActivity(user, asOfUtc);

        await _db.SaveChangesAsync(ct);

        // PR 67 — alimenta a meta coletiva diária da caixinha. Best-effort:
        // o social não pode quebrar o fluxo de estudo.
        try
        {
            await _caixinha.ContributeAsync(userId, rewards.Xp, asOfUtc, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao contribuir XP pra meta da caixinha (user {UserId}).", userId);
        }

        return new UserGamificationSnapshot(
            Xp:         user.Xp,
            Coins:      user.Coins,
            Stars:      user.Stars,
            Lives:      user.Lives,
            StreakDays: user.StreakDays);
    }
}
