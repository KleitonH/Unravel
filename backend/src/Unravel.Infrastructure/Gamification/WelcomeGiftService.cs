using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Gamification.Ports;
using Unravel.Application.Notifications.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Gamification;

/// <summary>
/// Concede o set "Mestre dos Gatos" (brinde de pré-registro) ao novo usuário
/// e cria a notificação de bônus. Idempotente por peça (não duplica se já
/// possuída) e resiliente: qualquer exceção é engolida para não quebrar o
/// cadastro.
/// </summary>
public sealed class WelcomeGiftService(
    ApplicationDbContext db,
    INotificationService notifications,
    ILogger<WelcomeGiftService> logger) : IWelcomeGiftService
{
    public async Task GrantAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            // As 4 peças do set (uma por slot — Accessory/Hat/Fur/Expression),
            // localizadas pelo AssetSlug semeado em GamificationSeeder.
            var pieces = await db.NaviCosmetic
                .Where(c => GamificationSeeder.MestreSetSlugs.Contains(c.AssetSlug))
                .Select(c => c.Id)
                .ToListAsync(ct);

            if (pieces.Count == 0)
            {
                logger.LogWarning("Set Mestre dos Gatos não encontrado no catálogo — brinde não concedido a {UserId}.", userId);
                return;
            }

            // Não duplica peças já possuídas (idempotente).
            var owned = await db.UserCosmetic
                .Where(uc => uc.UserId == userId && pieces.Contains(uc.CosmeticId))
                .Select(uc => uc.CosmeticId)
                .ToListAsync(ct);

            var toGrant = pieces.Where(id => !owned.Contains(id)).ToList();
            if (toGrant.Count > 0)
            {
                // Concedidas na coleção, porém NÃO equipadas — o aluno escolhe
                // se e quando vestir cada peça na Toca do NAVI.
                db.UserCosmetic.AddRange(toGrant.Select(id => new UserCosmetic
                {
                    UserId = userId, CosmeticId = id, IsEquipped = false, AcquiredAt = DateTime.UtcNow,
                }));
                await db.SaveChangesAsync(ct);
            }

            await notifications.CreateAsync(
                userId,
                NotificationType.Welcome,
                "Bem-vindo ao Unravel! 🎁",
                "Você ganhou o set Mestre dos Gatos como brinde de pré-registro. Equipe as peças no seu NAVI pela Toca!",
                "/loja",
                ct);
        }
        catch (Exception ex)
        {
            // Best-effort: brinde nunca deve derrubar o cadastro.
            logger.LogError(ex, "Falha ao conceder o brinde de boas-vindas a {UserId}.", userId);
        }
    }
}
