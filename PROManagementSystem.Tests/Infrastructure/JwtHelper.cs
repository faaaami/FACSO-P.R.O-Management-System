using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Mints JWTs in the same shape the production <c>JwtTokenService</c>
/// produces (same claims, issuer, audience and signing key) so the real
/// JwtBearer middleware validates them. Used by the real-auth tests.
/// </summary>
public static class JwtHelper
{
    public static string MintToken(
        Guid userId,
        string role,
        string email,
        string fullName,
        string? clientCompanyId = null,
        DateTime? expiresUtc = null)
    {
        var claims = new List<Claim>
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                userId.ToString()),
            new Claim(
                JwtRegisteredClaimNames.Email,
                email),
            new Claim(
                ClaimTypes.NameIdentifier,
                userId.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role)
        };

        if (!string.IsNullOrWhiteSpace(clientCompanyId))
        {
            claims.Add(new Claim("client_company_id", clientCompanyId));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(DatabaseFixture.TestJwtSecret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: DatabaseFixture.TestJwtIssuer,
            audience: DatabaseFixture.TestJwtAudience,
            claims: claims,
            expires: expiresUtc ?? DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string MintExpiredToken(
        Guid userId,
        string role,
        string email,
        string fullName,
        string? clientCompanyId = null)
        => MintToken(
            userId, role, email, fullName, clientCompanyId,
            expiresUtc: DateTime.UtcNow.AddHours(-2));

    public static string MintTokenWithWrongSecret(
        Guid userId,
        string role,
        string email,
        string fullName,
        string? clientCompanyId = null)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role)
        };

        if (!string.IsNullOrWhiteSpace(clientCompanyId))
        {
            claims.Add(new Claim("client_company_id", clientCompanyId));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("a-different-secret-key-far-away-from-u"));

        var token = new JwtSecurityToken(
            issuer: DatabaseFixture.TestJwtIssuer,
            audience: DatabaseFixture.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
