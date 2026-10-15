namespace PROManagementSystem.Application.Common.Interfaces;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);

/// <summary>
/// Transport for outgoing email (SMTP in production, log-only in
/// Development).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
