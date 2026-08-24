using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);

    string GenerateRefreshToken();

    bool ValidateRefreshToken(User user, string refreshToken);
}