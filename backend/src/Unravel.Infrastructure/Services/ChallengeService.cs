using Microsoft.EntityFrameworkCore;
using Unravel.Application.Services;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Services;

public class ChallengeService : IChallengeService
{
    private readonly ApplicationDbContext _db;

    public ChallengeService(ApplicationDbContext db) => _db = db;

    // ── Helpers ──────────────────────────────────────────────────

    private static string LevelLabel(DifficultyLevel l) => l switch
    {
        DifficultyLevel.Beginner     => "Iniciante",
        DifficultyLevel.Intermediate => "Intermediário",
        DifficultyLevel.Advanced     => "Avançado",
        _                            => "Iniciante"
    };

    private static ChallengeResponse ToResponse(Challenge c) => new(
        c.Id, c.TrailId, c.Title, c.Description,
        LevelLabel(c.Level), c.XpReward, c.CoinsReward, c.IsDailyChallenge,
        c.Options.OrderBy(o => o.Order).Select(o => new OptionResponse(o.Id, o.Text, o.Order))
    );

    private static string DailyLoginRewardDescription(int cycleDay) => cycleDay switch
    {
        1 or 2 or 3 => "❤️ +1 Vida bônus",
        4 or 5      => "🪙 +25 Moedas",
        6           => "⭐ Booster de XP por 24h",
        7           => "🎁 Recompensa semanal especial",
        _           => "🐱 Bem-vindo de volta!"
    };

    // ── CRUD ─────────────────────────────────────────────────────

    public async Task<IEnumerable<ChallengeResponse>> GetByTrailAsync(int trailId)
    {
        var challenges = await _db.Challenge
            .Where(c => c.TrailId == trailId && c.IsActive)
            .Include(c => c.Options)
            .OrderBy(c => c.Level)
            .ToListAsync();
        return challenges.Select(ToResponse);
    }

    public async Task<ChallengeResponse?> GetByIdAsync(int id)
    {
        var c = await _db.Challenge
            .Include(c => c.Options)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
        return c is null ? null : ToResponse(c);
    }

    public async Task<ChallengeResponse> CreateAsync(CreateChallengeRequest dto)
    {
        var challenge = new Challenge
        {
            TrailId          = dto.TrailId,
            Title            = dto.Title,
            Description      = dto.Description,
            Level            = (DifficultyLevel)dto.Level,
            XpReward         = dto.XpReward,
            CoinsReward      = dto.CoinsReward,
            IsDailyChallenge = dto.IsDailyChallenge,
            Options          = dto.Options.Select(o => new ChallengeOption
            {
                Text      = o.Text,
                IsCorrect = o.IsCorrect,
                Order     = o.Order
            }).ToList()
        };
        _db.Challenge.Add(challenge);
        await _db.SaveChangesAsync();
        return ToResponse(challenge);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var c = await _db.Challenge.FindAsync(id);
        if (c is null) return false;
        c.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Status diário ─────────────────────────────────────────────

    public async Task<DailyStatusResponse> GetDailyStatusAsync(Guid userId)
    {
        var user = await _db.User.FindAsync(userId);
        if (user is null) return new(false, null, false, 0, 0, "");

        await ProcessDailyLoginAsync(user);
        await _db.SaveChangesAsync();

        var daily = await _db.Challenge
            .Include(c => c.Options)
            .FirstOrDefaultAsync(c => c.IsDailyChallenge && c.IsActive);

        bool alreadyDone = false;
        if (daily is not null)
        {
            var today = DateTime.UtcNow.Date;
            alreadyDone = await _db.UserChallenge
                .AnyAsync(uc => uc.UserId == userId
                             && uc.ChallengeId == daily.Id
                             && uc.IsCompleted
                             && uc.CompletedAt!.Value.Date == today);
        }

        return new(
            daily is not null,
            daily is null ? null : ToResponse(daily),
            alreadyDone,
            user.StreakDays,
            user.LoginCycleDay,
            DailyLoginRewardDescription(user.LoginCycleDay)
        );
    }

    // ── Submissão e correção automática ───────────────────────────

    public async Task<ChallengeResultResponse> SubmitAsync(Guid userId, SubmitChallengeRequest dto)
    {
        var user = await _db.User.FindAsync(userId)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        var challenge = await _db.Challenge
            .Include(c => c.Options)
            .FirstOrDefaultAsync(c => c.Id == dto.ChallengeId && c.IsActive)
            ?? throw new KeyNotFoundException("Desafio não encontrado.");

        var correctIds = challenge.Options
            .Where(o => o.IsCorrect)
            .Select(o => o.Id)
            .ToHashSet();

        var answerResults = dto.Answers.Select(a => new AnswerResultDto(
            a.SelectedOptionId,
            correctIds.Contains(a.SelectedOptionId),
            challenge.Options.FirstOrDefault(o => o.IsCorrect)?.Text ?? ""
        )).ToList();

        var correctCount = answerResults.Count(r => r.IsCorrect);
        var isPerfect    = correctCount == answerResults.Count && answerResults.Count > 0;

        var ratio       = answerResults.Count > 0 ? (double)correctCount / answerResults.Count : 0;
        var xpEarned    = (int)(challenge.XpReward * ratio);
        var coinsEarned = isPerfect ? challenge.CoinsReward * 2 : (int)(challenge.CoinsReward * ratio);

        user.Xp    += xpEarned;
        user.Coins += coinsEarned;
        if (isPerfect) user.Stars += 1;

        await UpdateStreakAsync(user, hasActivity: true);

        _db.UserChallenge.Add(new UserChallenge
        {
            UserId            = userId,
            ChallengeId       = dto.ChallengeId,
            CorrectAnswers    = correctCount,
            TotalQuestions    = answerResults.Count,
            XpEarned          = xpEarned,
            CoinsEarned       = coinsEarned,
            IsCompleted       = true,
            IsPerfect         = isPerfect,
            AvgResponseTimeMs = dto.AvgResponseTimeMs,
            CompletedAt       = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var badgeEarned = await CheckStreakBadgesAsync(user);

        return new(
            xpEarned, coinsEarned, correctCount, answerResults.Count,
            isPerfect, user.StreakDays, user.Xp, user.Coins,
            badgeEarned, answerResults
        );
    }

    // ── Lógica de Streak ─────────────────────────────────────────

    private static Task UpdateStreakAsync(User user, bool hasActivity)
    {
        var today    = DateTime.UtcNow.Date;
        var lastDate = user.LastActivityDate?.Date;

        if (hasActivity)
        {
            if (lastDate == null || lastDate < today.AddDays(-1))
                user.StreakDays = 1;
            else if (lastDate == today.AddDays(-1))
            {
                user.StreakDays++;
                if (user.StreakDays > user.LongestStreak)
                    user.LongestStreak = user.StreakDays;
            }
            user.LastActivityDate = DateTime.UtcNow;
        }
        else if (lastDate < today.AddDays(-1))
        {
            user.StreakDays = 0;
        }

        return Task.CompletedTask;
    }

    // ── Login diário (ciclo semanal) ─────────────────────────────

    private static Task ProcessDailyLoginAsync(User user)
    {
        var today    = DateTime.UtcNow.Date;
        var lastDate = user.LastLoginDate?.Date;
        if (lastDate == today) return Task.CompletedTask;

        user.LastLoginDate = DateTime.UtcNow;

        if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday && lastDate?.DayOfWeek != DayOfWeek.Monday)
            user.LoginCycleDay = 1;
        else
            user.LoginCycleDay = Math.Min(user.LoginCycleDay + 1, 7);

        switch (user.LoginCycleDay)
        {
            case 1: case 2: case 3:
                user.Lives = Math.Min(user.Lives + 1, 10);
                break;
            case 4: case 5:
                user.Coins += 25;
                break;
            case 6:
                user.Xp += 50;
                break;
            case 7:
                user.Coins += 100;
                user.Stars  += 1;
                break;
        }

        return Task.CompletedTask;
    }

    // ── Badges de streak ─────────────────────────────────────────

    private async Task<string?> CheckStreakBadgesAsync(User user)
    {
        var milestones = new Dictionary<int, string>
        {
            {   7, "🔥 Ofensiva de 7 dias"  },
            {  14, "⚡ Ofensiva de 14 dias" },
            {  30, "🌙 Ofensiva de 30 dias" },
            {  60, "🏆 Ofensiva de 60 dias" },
            { 100, "💎 Pelagem de Platina"  }
        };

        if (!milestones.TryGetValue(user.StreakDays, out var badgeName)) return null;

        var badge = await _db.Badge.FirstOrDefaultAsync(b => b.Name == badgeName);
        if (badge is null) return null;

        var alreadyHas = await _db.UserBadge.AnyAsync(ub => ub.UserId == user.Id && ub.BadgeId == badge.Id);
        if (alreadyHas) return null;

        _db.UserBadge.Add(new UserBadge { UserId = user.Id, BadgeId = badge.Id });
        await _db.SaveChangesAsync();
        return badgeName;
    }
}
