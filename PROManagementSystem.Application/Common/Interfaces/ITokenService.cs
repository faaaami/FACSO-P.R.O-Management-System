using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface ITokenService
{
    /// <summary>Claim present while the user must change their password.</summary>
    public const string PasswordChangeRequiredClaim = "pwd_change_required";

    TimeSpan AccessTokenLifetime { get; }

    TimeSpan RefreshTokenLifetime { get; }

    string GenerateAccessToken(User user);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);

    bool ValidateRefreshToken(User user, string refreshToken);
}
