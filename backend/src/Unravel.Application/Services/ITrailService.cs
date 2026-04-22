using Unravel.Application.DTOs;

namespace Unravel.Application.Services;

public interface ITrailService
{
    Task<IEnumerable<TrailResponse>> GetAllAsync(Guid userId);
    Task<TrailResponse?>             GetByIdAsync(int id, Guid userId);
    Task<TrailResponse>              CreateAsync(CreateTrailRequest dto);
    Task<TrailResponse?>             UpdateAsync(int id, UpdateTrailRequest dto);
    Task<bool>                       DeleteAsync(int id);

    Task<ProgressResponse>  EnrollAsync(Guid userId, int trailId);
    Task<ProgressResponse?> GetProgressAsync(Guid userId, int trailId);
    Task<ProgressResponse>  CompleteContentAsync(Guid userId, int contentId);

    Task<IEnumerable<ContentResponse>> GetSequenceAsync(Guid userId, int trailId);
}
