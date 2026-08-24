namespace PROManagementSystem.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenResponseDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);