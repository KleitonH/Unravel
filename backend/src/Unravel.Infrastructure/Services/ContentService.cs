using Microsoft.EntityFrameworkCore;
using Unravel.Application.DTOs;
using Unravel.Application.Services;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Services;

public class ContentService : IContentService
{
    private readonly ApplicationDbContext _db;
    public ContentService(ApplicationDbContext db) => _db = db;

    private static string LevelLabel(DifficultyLevel l) => l switch
    {
        DifficultyLevel.Beginner     => "Iniciante",
        DifficultyLevel.Intermediate => "Intermediário",
        DifficultyLevel.Advanced     => "Avançado",
        _                            => "Iniciante"
    };

    private static string TypeLabel(ContentType t) => t switch
    {
        ContentType.Article  => "Artigo",
        ContentType.Video    => "Vídeo",
        ContentType.Exercise => "Exercício",
        _                    => "Artigo"
    };

    public async Task<IEnumerable<ContentResponse>> GetByTrailAsync(int trailId, Guid userId)
    {
        var contents = await _db.Contents
            .Where(c => c.TrailId == trailId && c.IsActive)
            .OrderBy(c => c.Level).ThenBy(c => c.Order)
            .ToListAsync();

        var completedIds = (await _db.UserContents
            .Where(uc => uc.UserId == userId && uc.Content.TrailId == trailId && uc.IsCompleted)
            .Select(uc => uc.ContentId)
            .ToListAsync()).ToHashSet();

        return contents.Select(c => new ContentResponse(
            c.Id, c.TrailId, c.Title, c.Body, c.ExternalUrl,
            TypeLabel(c.Type), LevelLabel(c.Level), c.Order,
            completedIds.Contains(c.Id)
        ));
    }

    public async Task<ContentResponse?> GetByIdAsync(int id, Guid userId)
    {
        var c = await _db.Contents.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
        if (c is null) return null;

        var isCompleted = await _db.UserContents
            .AnyAsync(uc => uc.UserId == userId && uc.ContentId == id && uc.IsCompleted);

        return new ContentResponse(c.Id, c.TrailId, c.Title, c.Body, c.ExternalUrl,
            TypeLabel(c.Type), LevelLabel(c.Level), c.Order, isCompleted);
    }

    public async Task<ContentResponse> CreateAsync(CreateContentRequest dto)
    {
        var content = new Content
        {
            TrailId     = dto.TrailId,
            Title       = dto.Title,
            Body        = dto.Body,
            ExternalUrl = dto.ExternalUrl,
            Type        = (ContentType)dto.Type,
            Level       = (DifficultyLevel)dto.Level,
            Order       = dto.Order
        };
        _db.Contents.Add(content);
        await _db.SaveChangesAsync();
        return new ContentResponse(content.Id, content.TrailId, content.Title, content.Body,
            content.ExternalUrl, TypeLabel(content.Type), LevelLabel(content.Level), content.Order, false);
    }

    public async Task<ContentResponse?> UpdateAsync(int id, UpdateContentRequest dto, Guid userId)
    {
        var content = await _db.Contents.FindAsync(id);
        if (content is null) return null;

        if (dto.Title       is not null) content.Title       = dto.Title;
        if (dto.Body        is not null) content.Body        = dto.Body;
        if (dto.ExternalUrl is not null) content.ExternalUrl = dto.ExternalUrl;
        if (dto.Type        is not null) content.Type        = (ContentType)dto.Type;
        if (dto.Level       is not null) content.Level       = (DifficultyLevel)dto.Level;
        if (dto.Order       is not null) content.Order       = dto.Order.Value;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id, userId);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var content = await _db.Contents.FindAsync(id);
        if (content is null) return false;
        content.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }
}
