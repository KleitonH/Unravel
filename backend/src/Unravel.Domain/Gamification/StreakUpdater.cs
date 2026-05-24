using Unravel.Domain.Entities;

namespace Unravel.Domain.Gamification;

/// <summary>
/// Lógica pura de atualização de streak. Extraída do
/// <c>ChallengeService.UpdateStreakAsync</c> existente para reuso entre o
/// fluxo de Challenge curado (legado) e o submit de GeneratedChallenge
/// (PR 13). Aceita o User mutável porque é a forma idiomática do EF — o
/// chamador faz SaveChanges depois.
///
/// <para>Regras (do doc Ofensiva e código pré-existente):</para>
/// <list type="bullet">
///   <item>Sem atividade anterior, ou gap ≥ 2 dias: streak vai a 1 hoje.</item>
///   <item>Última atividade foi ontem: streak += 1, atualiza LongestStreak
///   se quebrou recorde.</item>
///   <item>Última atividade foi hoje: streak intocado (já contou).</item>
/// </list>
/// <para><c>LastActivityDate</c> sempre vira "agora" em qualquer atividade.</para>
/// </summary>
public static class StreakUpdater
{
    public static void RegisterActivity(User user, DateTime nowUtc)
    {
        var today    = nowUtc.Date;
        var lastDate = user.LastActivityDate?.Date;

        if (lastDate is null || lastDate < today.AddDays(-1))
        {
            // Primeira atividade ou voltou depois de 2+ dias.
            user.StreakDays = 1;
        }
        else if (lastDate == today.AddDays(-1))
        {
            user.StreakDays += 1;
            if (user.StreakDays > user.LongestStreak)
                user.LongestStreak = user.StreakDays;
        }
        // Se lastDate == today, streak já contou hoje — não muda.

        user.LastActivityDate = nowUtc;
    }
}
