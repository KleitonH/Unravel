using Unravel.Domain.Gamification;

namespace Unravel.Application.Gamification.Ports;

/// <summary>
/// Aplica recompensas e penalidades a um usuário, e devolve o estado
/// resultante para a UI. Encapsula o load + mutate + save num único
/// método transacional — o chamador (use case) não precisa pensar em
/// concorrência ou ordem.
///
/// <para>Lives são capadas em [0, MaxLives] dentro da implementação.
/// Streak é atualizada via <see cref="StreakUpdater"/> sempre que há
/// atividade (independente de acerto/erro — o doc trata cada submit
/// como sinal de engajamento).</para>
/// </summary>
public interface IUserGamificationGateway
{
    /// <summary>Aplica rewards + atualiza streak + persiste. Retorna o
    /// snapshot pós-aplicação para a UI exibir.</summary>
    Task<UserGamificationSnapshot> ApplyAsync(
        Guid userId, SubmissionRewards rewards, DateTime asOfUtc, CancellationToken ct = default);
}

/// <summary>Estado do usuário pós-aplicação — o que a UI precisa exibir
/// no feedback do quiz ("Você ganhou X XP, perdeu 1 vida", etc).</summary>
public sealed record UserGamificationSnapshot(
    int Xp,
    int Coins,
    int Stars,
    int Lives,
    int StreakDays);
