namespace Unravel.Application.DTOs;

public record TrailResponse(
    int    Id,
    string Name,
    string Description,
    string Icon,
    string AccentColor,
    string Level,
    int    TotalContents,
    int    UserProgress
);

public record CreateTrailRequest(
    string Name,
    string Description,
    string Icon,
    string AccentColor,
    int    Level
);

public record UpdateTrailRequest(
    string? Name,
    string? Description,
    string? Icon,
    string? AccentColor,
    int?    Level
);

public record ContentResponse(
    int     Id,
    int     TrailId,
    string  Title,
    string  Body,
    string? ExternalUrl,
    string  Type,
    string  Level,
    int     Order,
    bool    IsCompleted
);

public record CreateContentRequest(
    int     TrailId,
    string  Title,
    string  Body,
    string? ExternalUrl,
    int     Type,
    int     Level,
    int     Order
);

public record UpdateContentRequest(
    string? Title,
    string? Body,
    string? ExternalUrl,
    int?    Type,
    int?    Level,
    int?    Order
);

public record EnrollRequest(int TrailId);

public record ProgressResponse(
    int    TrailId,
    string TrailName,
    int    Progress,
    int    CompletedContents,
    int    TotalContents,
    bool   IsCompleted
);

public record CompleteContentRequest(int ContentId);
