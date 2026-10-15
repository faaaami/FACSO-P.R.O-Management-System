using System.Security.Cryptography;
using System.Text;

namespace PROManagementSystem.Application.Common.Helpers;

/// <summary>
/// Generates and hashes the single-use tokens sent by email (verification,
/// password reset, invitation). Tokens are URL-safe so they can be placed in
/// a link without encoding; only the hash is stored.
/// </summary>
public static class AccountTokens
{
    public static readonly TimeSpan PasswordResetLifetime =
        TimeSpan.FromHours(1);

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Hash(string token)
        => Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// A password hash input nobody knows, for accounts that must set their
    /// own password through an emailed link before they can sign in.
    /// </summary>
    public static string UnusablePassword()
        => Generate() + Generate();
}
