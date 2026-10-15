namespace PROManagementSystem.Application.Auth.Commands.RefreshToken;

/// <param name="ExpiresIn">Access token lifetime in seconds.</param>
public sealed record RefreshTokenResponseDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    bool MustChangePassword
);
