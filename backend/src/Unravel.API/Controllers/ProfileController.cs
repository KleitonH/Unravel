using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unravel.Infrastructure.Persistence;

namespace Unravel.API.Controllers;

public record ProfileResponse(
    Guid   Id,
    string Name,
    string Email,
    int    Xp,
    int    Coins,
    int    Stars,
    int    Lives,
    int    StreakDays,
    int    LongestStreak,
    int    LoginCycleDay,
    string? ActiveTitle,
    IEnumerable<BadgeDto>    Badges,
    IEnumerable<CosmeticDto> Cosmetics
);

public record BadgeDto(int Id, string Name, string Description, string Icon, string Category, string EarnedAt);
public record CosmeticDto(int Id, string Name, string Type, string Rarity, bool IsEquipped);
public record UpdateTitleRequest(string? Title);

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(ApplicationDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var user = await db.User
            .Include(u => u.Badges).ThenInclude(ub => ub.Badge)
            .Include(u => u.Cosmetics).ThenInclude(uc => uc.Cosmetic)
            .FirstOrDefaultAsync(u => u.Id == UserId);

        if (user is null) return NotFound();

        return Ok(new ProfileResponse(
            user.Id, user.Name, user.Email,
            user.Xp, user.Coins, user.Stars, user.Lives,
            user.StreakDays, user.LongestStreak, user.LoginCycleDay,
            user.ActiveTitle,
            user.Badges.Select(ub => new BadgeDto(
                ub.Badge.Id, ub.Badge.Name, ub.Badge.Description,
                ub.Badge.Icon, ub.Badge.Category.ToString(),
                ub.EarnedAt.ToString("dd/MM/yyyy")
            )),
            user.Cosmetics.Select(uc => new CosmeticDto(
                uc.Cosmetic.Id, uc.Cosmetic.Name,
                uc.Cosmetic.Type.ToString(), uc.Cosmetic.Rarity.ToString(),
                uc.IsEquipped
            ))
        ));
    }

    // PUT /api/profile/title
    [HttpPut("title")]
    public async Task<IActionResult> UpdateTitle([FromBody] UpdateTitleRequest dto)
    {
        var user = await db.User.FindAsync(UserId);
        if (user is null) return NotFound();

        user.ActiveTitle = dto.Title;
        await db.SaveChangesAsync();
        return Ok(new { activeTitle = user.ActiveTitle });
    }

    // PUT /api/profile/cosmetic/{cosmeticId}/equip
    [HttpPut("cosmetic/{cosmeticId:int}/equip")]
    public async Task<IActionResult> EquipCosmetic(int cosmeticId)
    {
        var userCosmetic = await db.UserCosmetic
            .FirstOrDefaultAsync(uc => uc.UserId == UserId && uc.CosmeticId == cosmeticId);

        if (userCosmetic is null)
            return NotFound(new { message = "Você não possui este cosmético." });

        // Desequipa todos os do mesmo tipo
        var cosmetic = await db.NaviCosmetic.FindAsync(cosmeticId);
        if (cosmetic is not null)
        {
            var sameType = await db.UserCosmetic
                .Include(uc => uc.Cosmetic)
                .Where(uc => uc.UserId == UserId && uc.Cosmetic.Type == cosmetic.Type && uc.IsEquipped)
                .ToListAsync();
            sameType.ForEach(uc => uc.IsEquipped = false);
        }

        userCosmetic.IsEquipped = true;
        await db.SaveChangesAsync();
        return Ok(new { message = "Cosmético equipado com sucesso." });
    }

    // GET /api/profile/ranking
    [HttpGet("ranking")]
    public async Task<IActionResult> GetRanking([FromQuery] int top = 10)
    {
        var ranking = await db.User
            .OrderByDescending(u => u.Xp)
            .Take(top)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Xp,
                u.StreakDays,
                u.ActiveTitle,
                BadgeCount = u.Badges.Count
            })
            .ToListAsync();

        return Ok(ranking);
    }
}
