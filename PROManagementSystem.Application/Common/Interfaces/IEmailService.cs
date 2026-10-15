namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>
/// Builds and sends the account emails. Each token is passed in raw so it can
/// be embedded in the link; implementations must never log it.
/// </summary>
public interface IEmailService
{
    Task SendEmailVerificationAsync(
        string email,
        string fullName,
        string verificationToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string email,
        string fullName,
        string resetToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task SendClientInvitationAsync(
        string email,
        string fullName,
        string companyName,
        string invitationToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);
}
