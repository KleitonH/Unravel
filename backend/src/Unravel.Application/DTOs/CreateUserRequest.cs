namespace Unravel.Application.DTOs;

public record CreateUserRequest(
    string Name,
    string Email,
    string Password
);
