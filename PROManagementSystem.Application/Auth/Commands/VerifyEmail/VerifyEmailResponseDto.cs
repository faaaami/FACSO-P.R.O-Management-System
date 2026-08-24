namespace PROManagementSystem.Application.Auth.Commands.VerifyEmail;

public sealed record VerifyEmailResponseDto(
    Guid UserId,
    string Email,
    bool EmailVerified
);