using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Email;

/// <summary>
/// Development-only sender that records that an email would have been sent.
/// It logs the recipient and subject but never the body, because the body
/// carries single-use tokens. To read real emails locally, configure Smtp to
/// point at a local catcher such as Mailpit.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Development email not sent (no SMTP configured): " +
            "'{Subject}' to {Recipient}.",
            message.Subject,
            message.To);

        return Task.CompletedTask;
    }
}

public static class EmailSenderSelection
{
    /// <summary>
    /// SMTP whenever it is configured; otherwise the log-only sender, which
    /// is allowed in Development only.
    /// </summary>
    public static Type Select(bool isDevelopment, SmtpOptions smtp)
    {
        if (smtp.IsConfigured)
        {
            return typeof(SmtpEmailSender);
        }

        if (isDevelopment)
        {
            return typeof(LoggingEmailSender);
        }

        throw new InvalidOperationException(
            "Smtp:Host and Smtp:FromAddress must be configured outside " +
            "Development. The log-only email sender is for Development only.");
    }
}
