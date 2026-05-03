using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.API.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────

public record BadgeDto(int Id, string Name, string Description, string Icon, string Category, string EarnedAt);
public record CosmeticDto(int Id, string Name, string Type, string Rarity, bool IsEquipped);
public record UpdateTitleRequest(string? Title);
public record TrailProgressDto(int TrailId, string TrailName, int Progress, bool IsCompleted);

public record StudentProfileResponse(
    Guid    Id,
    string  Name,
    string  Email,
    string  Role,
    int     Xp,
    int     Coins,
    int     Stars,
    int     Lives,
    int     StreakDays,
    int     LongestStreak,
    int     LoginCycleDay,
    string? ActiveTitle,
    IEnumerable<BadgeDto>        Badges,
    IEnumerable<CosmeticDto>     Cosmetics,
    IEnumerable<TrailProgressDto> TrailProgress
);

public record TrailSummaryDto(int Id, string Name, int ContentCount, int ChallengeCount, int EnrolledCount);
public record PlatformMetricsDto(int TotalStudents, int TotalTrails, int TotalContents, int TotalChallenges, long TotalXpDistributed);

public record ModeratorProfileResponse(
    Guid   Id,
    string Name,
    string Email,
    string Role,
    PlatformMetricsDto           Metrics,
    IEnumerable<TrailSummaryDto> Trails
);

// ── Controller ───────────────────────────────────────────────────

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(ApplicationDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string UserRole => User.FindFirstValue(ClaimTypes.Role) ?? nameof(Role.Student);

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        if (UserRole == nameof(Role.Moderator))
            return Ok(await BuildModeratorProfileAsync());

        return Ok(await BuildStudentProfileAsync());
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

    // ── Private builders ─────────────────────────────────────────

    private async Task<StudentProfileResponse> BuildStudentProfileAsync()
    {
        var user = await db.User
            .Include(u => u.Badges).ThenInclude(ub => ub.Badge)
            .Include(u => u.Cosmetics).ThenInclude(uc => uc.Cosmetic)
            .FirstOrDefaultAsync(u => u.Id == UserId)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        var trailProgress = await db.UserTrail
            .Include(ut => ut.Trail)
            .Where(ut => ut.UserId == UserId)
            .Select(ut => new TrailProgressDto(
                ut.TrailId,
                ut.Trail.Name,
                ut.Progress,
                ut.Progress >= 100
            ))
            .ToListAsync();

        return new StudentProfileResponse(
            user.Id, user.Name, user.Email, user.Role.ToString(),
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
            )),
            trailProgress
        );
    }

    private async Task<ModeratorProfileResponse> BuildModeratorProfileAsync()
    {
        var user = await db.User.FindAsync(UserId)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        var metrics = new PlatformMetricsDto(
            TotalStudents:    await db.User.CountAsync(u => u.Role == Role.Student && u.IsActive),
            TotalTrails:      await db.Trail.CountAsync(t => t.IsActive),
            TotalContents:    await db.Content.CountAsync(c => c.IsActive),
            TotalChallenges:  await db.Challenge.CountAsync(c => c.IsActive),
            TotalXpDistributed: await db.User.SumAsync(u => (long)u.Xp)
        );

        var trails = await db.Trail
            .Where(t => t.IsActive)
            .Select(t => new TrailSummaryDto(
                t.Id,
                t.Name,
                t.Contents.Count(c => c.IsActive),
                db.Challenge.Count(c => c.TrailId == t.Id && c.IsActive),
                t.UserTrails.Count
            ))
            .ToListAsync();

        return new ModeratorProfileResponse(
            user.Id, user.Name, user.Email, user.Role.ToString(),
            metrics, trails
        );
    }
}
