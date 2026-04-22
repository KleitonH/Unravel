using Microsoft.EntityFrameworkCore;
using Unravel.Application.DTOs;
using Unravel.Application.Services;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Services;

public class TrailService : ITrailService
{
    private readonly ApplicationDbContext _db;

    public TrailService(ApplicationDbContext db) => _db = db;

    private static string LevelLabel(DifficultyLevel l) => l switch
    {
        DifficultyLevel.Beginner     => "Iniciante",
        DifficultyLevel.Intermediate => "Intermediário",
        DifficultyLevel.Advanced     => "Avançado",
        _                            => "Iniciante"
    };

    private static string ContentTypeLabel(ContentType t) => t switch
    {
        ContentType.Article  => "Artigo",
        ContentType.Video    => "Vídeo",
        ContentType.Exercise => "Exercício",
        _                    => "Artigo"
    };

    private async Task<int> CalcProgressAsync(Guid userId, int trailId)
    {
        var total = await _db.Contents.CountAsync(c => c.TrailId == trailId && c.IsActive);
        if (total == 0) return 0;
        var completed = await _db.UserContents
            .CountAsync(uc => uc.UserId == userId && uc.Content.TrailId == trailId && uc.IsCompleted);
        return (int)Math.Round(completed * 100.0 / total);
    }

    public async Task<IEnumerable<TrailResponse>> GetAllAsync(Guid userId)
    {
        var trails = await _db.Trails
            .Where(t => t.IsActive)
            .Include(t => t.Contents)
            .Include(t => t.UserTrails.Where(ut => ut.UserId == userId))
            .OrderBy(t => t.Level)
            .ToListAsync();

        return trails.Select(t => new TrailResponse(
            t.Id, t.Name, t.Description, t.Icon, t.AccentColor,
            LevelLabel(t.Level),
            t.Contents.Count(c => c.IsActive),
            t.UserTrails.FirstOrDefault()?.Progress ?? -1
        ));
    }

    public async Task<TrailResponse?> GetByIdAsync(int id, Guid userId)
    {
        var t = await _db.Trails
            .Include(t => t.Contents)
            .Include(t => t.UserTrails.Where(ut => ut.UserId == userId))
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

        if (t is null) return null;

        return new TrailResponse(
            t.Id, t.Name, t.Description, t.Icon, t.AccentColor,
            LevelLabel(t.Level),
            t.Contents.Count(c => c.IsActive),
            t.UserTrails.FirstOrDefault()?.Progress ?? -1
        );
    }

    public async Task<TrailResponse> CreateAsync(CreateTrailRequest dto)
    {
        var trail = new Trail
        {
            Name        = dto.Name,
            Description = dto.Description,
            Icon        = dto.Icon,
            AccentColor = dto.AccentColor,
            Level       = (DifficultyLevel)dto.Level
        };
        _db.Trails.Add(trail);
        await _db.SaveChangesAsync();
        return new TrailResponse(trail.Id, trail.Name, trail.Description,
            trail.Icon, trail.AccentColor, LevelLabel(trail.Level), 0, -1);
    }

    public async Task<TrailResponse?> UpdateAsync(int id, UpdateTrailRequest dto)
    {
        var trail = await _db.Trails.FindAsync(id);
        if (trail is null) return null;

        if (dto.Name        is not null) trail.Name        = dto.Name;
        if (dto.Description is not null) trail.Description = dto.Description;
        if (dto.Icon        is not null) trail.Icon        = dto.Icon;
        if (dto.AccentColor is not null) trail.AccentColor = dto.AccentColor;
        if (dto.Level       is not null) trail.Level       = (DifficultyLevel)dto.Level;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id, Guid.Empty);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var trail = await _db.Trails.FindAsync(id);
        if (trail is null) return false;
        trail.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ProgressResponse> EnrollAsync(Guid userId, int trailId)
    {
        var existing = await _db.UserTrails
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TrailId == trailId);

        if (existing is not null)
            existing.IsActive = true;
        else
            _db.UserTrails.Add(new UserTrail { UserId = userId, TrailId = trailId });

        await _db.SaveChangesAsync();
        return (await GetProgressAsync(userId, trailId))!;
    }

    public async Task<ProgressResponse?> GetProgressAsync(Guid userId, int trailId)
    {
        var trail = await _db.Trails
            .Include(t => t.Contents)
            .FirstOrDefaultAsync(t => t.Id == trailId && t.IsActive);
        if (trail is null) return null;

        var total     = trail.Contents.Count(c => c.IsActive);
        var completed = await _db.UserContents
            .CountAsync(uc => uc.UserId == userId && uc.Content.TrailId == trailId && uc.IsCompleted);
        var progress  = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total);

        return new ProgressResponse(trailId, trail.Name, progress, completed, total, progress == 100);
    }

    public async Task<ProgressResponse> CompleteContentAsync(Guid userId, int contentId)
    {
        var content = await _db.Contents.FindAsync(contentId)
            ?? throw new KeyNotFoundException("Conteúdo não encontrado.");

        var uc = await _db.UserContents
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ContentId == contentId);

        if (uc is null)
        {
            _db.UserContents.Add(new UserContent
            {
                UserId = userId, ContentId = contentId,
                IsCompleted = true, CompletedAt = DateTime.UtcNow
            });
        }
        else
        {
            uc.IsCompleted = true;
            uc.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var progress = await CalcProgressAsync(userId, content.TrailId);
        var ut = await _db.UserTrails
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TrailId == content.TrailId);

        if (ut is not null)
        {
            ut.Progress    = progress;
            ut.CompletedAt = progress == 100 ? DateTime.UtcNow : null;
            await _db.SaveChangesAsync();
        }

        return (await GetProgressAsync(userId, content.TrailId))!;
    }

    public async Task<IEnumerable<ContentResponse>> GetSequenceAsync(Guid userId, int trailId)
    {
        var contents = await _db.Contents
            .Where(c => c.TrailId == trailId && c.IsActive)
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Order)
            .ToListAsync();

        var completedIds = (await _db.UserContents
            .Where(uc => uc.UserId == userId && uc.Content.TrailId == trailId && uc.IsCompleted)
            .Select(uc => uc.ContentId)
            .ToListAsync()).ToHashSet();

        return contents.Select(c => new ContentResponse(
            c.Id, c.TrailId, c.Title, c.Body, c.ExternalUrl,
            ContentTypeLabel(c.Type),
            LevelLabel(c.Level),
            c.Order,
            completedIds.Contains(c.Id)
        ));
    }
}
