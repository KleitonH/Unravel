namespace Unravel.Application.DTOs;

public record UserResponse(
    Guid Id,
    string Name,
    string Email,
    bool IsActive,
    DateTime CreatedAt
);
