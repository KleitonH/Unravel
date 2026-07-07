using Microsoft.EntityFrameworkCore;
using Unravel.Application.Gamification.Ports;
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
/// <para>O gateway trata só a recompensa <b>individual</b> (XP/moedas/vidas/
/// streak). O progresso <b>social</b> (novelo das parcerias e meta da caixinha)
/// é dirigido pelas <b>missões diárias</b> — cada missão concluída credita os
/// dois via <see cref="IActivitySink"/>. Os use cases de submit emitem as
/// atividades; o gateway não fala mais com o social diretamente.</para>
/// </summary>
public sealed class UserGamificationGateway : IUserGamificationGateway
{
    private readonly ApplicationDbContext _db;
    /// <summary>Teto de vidas — canônico em <see cref="Domain.Gamification.LifeRefill"/> (UC34).</summary>
    public const int MaxLives = Domain.Gamification.LifeRefill.MaxLives;

    public UserGamificationGateway(ApplicationDbContext db)
    {
        _db = db;
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

        return new UserGamificationSnapshot(
            Xp:         user.Xp,
            Coins:      user.Coins,
            Stars:      user.Stars,
            Lives:      user.Lives,
            StreakDays: user.StreakDays);
    }
}
