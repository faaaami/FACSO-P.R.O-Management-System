using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Email;

public sealed class DevelopmentEmailService : IEmailService
{
    private readonly ILogger<DevelopmentEmailService> _logger;

    public DevelopmentEmailService(
        ILogger<DevelopmentEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            """
            EMAIL VERIFICATION
            ------------------
            Email: {Email}
            Verification Token: {VerificationToken}
            """,
            email,
            verificationToken);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
    string email,
    string resetToken,
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            """
        PASSWORD RESET
        ------------------
        Email: {Email}
        Reset Token: {ResetToken}
        """,
            email,
            resetToken);

        return Task.CompletedTask;
    }

    public Task SendClientInvitationAsync(
    string email,
    string fullName,
    string temporaryPassword,
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            """
        CLIENT INVITATION
        ------------------
        Name: {FullName}
        Email: {Email}
        Temporary Password: {TemporaryPassword}
        """,
            fullName,
            email,
            temporaryPassword);

        return Task.CompletedTask;
    }
}

