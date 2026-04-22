using Unravel.Application.DTOs;

namespace Unravel.Application.Services;

public interface IContentService
{
    Task<IEnumerable<ContentResponse>> GetByTrailAsync(int trailId, Guid userId);
    Task<ContentResponse?>             GetByIdAsync(int id, Guid userId);
    Task<ContentResponse>              CreateAsync(CreateContentRequest dto);
    Task<ContentResponse?>             UpdateAsync(int id, UpdateContentRequest dto, Guid userId);
    Task<bool>                         DeleteAsync(int id);
}
