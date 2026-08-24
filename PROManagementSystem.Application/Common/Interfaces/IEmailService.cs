namespace PROManagementSystem.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default);

    Task SendClientInvitationAsync(
        string email,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
}