namespace Unravel.Application.DTOs;

public record CreateUserRequest(
    string  Name,
    string  Email,
    string  Password,
    string? Role       = null,  // "moderator" solicita cadastro de educador; qualquer outro valor → aluno
    string? InviteCode = null   // exigido (e validado) quando Role = "moderator"
);
