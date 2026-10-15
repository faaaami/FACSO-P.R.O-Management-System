using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Common;

internal static class ClientInvitations
{
    /// <summary>
    /// Adds a new invitation token for <paramref name="user"/> to the context
    /// (the caller saves) and returns the raw token and its expiry.
    /// </summary>
    public static (string RawToken, DateTime ExpiresAt) Issue(
        IApplicationDbContext context,
        User user,
        AccountTokenOptions options,
        DateTime nowUtc)
    {
        var rawToken = AccountTokens.Generate();
        var expiresAt = nowUtc.AddHours(
            Math.Max(1, options.InvitationLifetimeHours));

        context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = AccountTokens.Hash(rawToken),
            Purpose = AccountTokenPurpose.Invitation,
            ExpiresAt = expiresAt,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        });

        return (rawToken, expiresAt);
    }

    /// <summary>
    /// Sends the invitation after the account is committed. A delivery
    /// failure is logged and reported to the caller rather than thrown, so
    /// the account is kept and the invitation can be resent.
    /// </summary>
    public static async Task<bool> TrySendAsync(
        IEmailService emailService,
        ILogger logger,
        User user,
        string companyName,
        string rawToken,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailService.SendClientInvitationAsync(
                user.Email,
                user.FullName,
                companyName,
                rawToken,
                expiresAt,
                cancellationToken);

            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to send the invitation email for user {UserId}.",
                user.Id);

            return false;
        }
    }
}
