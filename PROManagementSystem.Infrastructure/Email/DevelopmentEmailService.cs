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
            "Development email simulated: verification email for " +
            "{Email} was generated. The verification token is " +
            "intentionally never logged.",
            email);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
    string email,
    string resetToken,
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Development email simulated: password reset email for " +
            "{Email} was generated. The reset token is " +
            "intentionally never logged.",
            email);

        return Task.CompletedTask;
    }

public Task SendClientInvitationAsync(
    string email,
    string fullName,
    CancellationToken cancellationToken = default)
{
    _logger.LogInformation(
        "Development email simulated: client invitation for " +
        "{FullName} <{Email}> was generated. No credential is " +
        "transmitted: the Admin shares the password directly.",
        fullName,
        email);

    return Task.CompletedTask;
}
}
