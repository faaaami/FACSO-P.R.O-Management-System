using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Auth.Common;

/// <summary>
/// Shared redemption of password-setting tokens (password reset and client
/// invitation). Must be called inside a transaction: the token is claimed
/// with a conditional UPDATE so two concurrent requests with the same link
/// cannot both succeed.
/// </summary>
internal static class PasswordTokenRedemption
{
    public const string InvalidLinkMessage =
        "This link is invalid. Please request a new one.";

    public static async Task<User> RedeemAsync(
        IApplicationDbContext context,
        string rawToken,
        AccountTokenPurpose purpose,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var tokenHash = AccountTokens.Hash(rawToken.Trim());

        var token = await context.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash &&
                     x.Purpose == purpose,
                cancellationToken);

        if (token is null ||
            token.User.IsDeleted ||
            !token.User.IsActive)
        {
            throw new UnauthorizedAccessException(InvalidLinkMessage);
        }

        if (token.UsedAt.HasValue)
        {
            throw new UnauthorizedAccessException(
                "This link has already been used. Please request a new one.");
        }

        if (token.ExpiresAt <= nowUtc)
        {
            throw new UnauthorizedAccessException(
                "This link has expired. Please request a new one.");
        }

        var claimed = await context.PasswordResetTokens
            .Where(x => x.Id == token.Id && x.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.UsedAt, nowUtc)
                    .SetProperty(x => x.UpdatedAt, nowUtc),
                cancellationToken);

        if (claimed == 0)
        {
            throw new UnauthorizedAccessException(
                "This link has already been used. Please request a new one.");
        }

        // Any other outstanding link of the same kind is now stale.
        await context.PasswordResetTokens
            .Where(x => x.UserId == token.UserId &&
                        x.Purpose == purpose &&
                        x.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.UsedAt, nowUtc)
                    .SetProperty(x => x.UpdatedAt, nowUtc),
                cancellationToken);

        return token.User;
    }

    /// <summary>Applies a new password chosen through an emailed link.</summary>
    public static void SetPassword(
        User user,
        string passwordHash,
        DateTime nowUtc)
    {
        user.PasswordHash = passwordHash;
        // Using an emailed link proves control of the mailbox.
        user.EmailVerified = true;
        user.MustChangePassword = false;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = nowUtc;
    }

    /// <summary>Invalidates the user's unused tokens of one purpose.</summary>
    public static Task RevokeOutstandingAsync(
        IApplicationDbContext context,
        Guid userId,
        AccountTokenPurpose purpose,
        DateTime nowUtc,
        CancellationToken cancellationToken)
        => context.PasswordResetTokens
            .Where(x => x.UserId == userId &&
                        x.Purpose == purpose &&
                        x.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.UsedAt, nowUtc)
                    .SetProperty(x => x.UpdatedAt, nowUtc),
                cancellationToken);
}
