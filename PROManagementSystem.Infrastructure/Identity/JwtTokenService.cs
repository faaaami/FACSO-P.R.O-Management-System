using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PROManagementSystem.Infrastructure.Identity;

public sealed class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");

        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException(
                "JWT Secret is not configured.");

        var issuer = jwtSettings["Issuer"]
            ?? throw new InvalidOperationException(
                "JWT Issuer is not configured.");

        var audience = jwtSettings["Audience"]
            ?? throw new InvalidOperationException(
                "JWT Audience is not configured.");

        var expirationMinutes = int.Parse(
            jwtSettings["AccessTokenExpirationMinutes"] ?? "60");

        var claims = new List<Claim>
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Email,
                user.Email),

            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Name,
                user.FullName),

            new Claim(
                ClaimTypes.Role,
                user.Role.ToString())
        };

        if (user.ClientCompanyId.HasValue)
        {
            claims.Add(
                new Claim(
                    "client_company_id",
                    user.ClientCompanyId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToBase64String(digest);
    }

    public bool ValidateRefreshToken(
        User user,
        string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) ||
            string.IsNullOrWhiteSpace(user.RefreshToken) ||
            !user.RefreshTokenExpiry.HasValue ||
            user.RefreshTokenExpiry.Value <= DateTime.UtcNow)
        {
            return false;
        }

        var presentedDigest = SHA256.HashData(
            Encoding.UTF8.GetBytes(refreshToken));

        byte[] storedDigest;

        try
        {
            storedDigest = Convert.FromBase64String(user.RefreshToken);
        }
        catch (FormatException)
        {
            return false;
        }

        return presentedDigest.Length == storedDigest.Length
            && CryptographicOperations.FixedTimeEquals(
                presentedDigest,
                storedDigest);
    }
}