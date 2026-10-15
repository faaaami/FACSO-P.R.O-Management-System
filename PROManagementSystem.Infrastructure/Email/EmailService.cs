using System.Net;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Email;

/// <summary>
/// Composes the account emails and hands them to the configured
/// <see cref="IEmailSender"/>. Links point at the frontend
/// (App:FrontendBaseUrl), which posts the token back to the API.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly IEmailSender _sender;
    private readonly AppUrlOptions _urls;
    private readonly TimeProvider _clock;

    public EmailService(
        IEmailSender sender,
        IOptions<AppUrlOptions> urls,
        TimeProvider clock)
    {
        _sender = sender;
        _urls = urls.Value;
        _clock = clock;
    }

    public Task SendEmailVerificationAsync(
        string email,
        string fullName,
        string verificationToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
        => SendAsync(
            email,
            "Verify your email address",
            fullName,
            "Please confirm that this is your email address for the FASCO PRO portal.",
            "Verify email",
            BuildLink("verify-email", verificationToken),
            expiresAtUtc,
            "If you did not expect this email, you can ignore it.",
            cancellationToken);

    public Task SendPasswordResetAsync(
        string email,
        string fullName,
        string resetToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
        => SendAsync(
            email,
            "Reset your FASCO PRO password",
            fullName,
            "We received a request to reset the password for your FASCO PRO account.",
            "Choose a new password",
            BuildLink("reset-password", resetToken),
            expiresAtUtc,
            "The link works once. If you did not request a reset, ignore this email; " +
            "your password will not change.",
            cancellationToken);

    public Task SendClientInvitationAsync(
        string email,
        string fullName,
        string companyName,
        string invitationToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
        => SendAsync(
            email,
            "You're invited to the FASCO PRO client portal",
            fullName,
            $"FASCO Document Clearing has invited you to the client portal for {companyName}. " +
            "Set your password to activate your account.",
            "Set your password",
            BuildLink("accept-invitation", invitationToken),
            expiresAtUtc,
            "The link works once. After you set your password, you can sign in " +
            "as soon as FASCO approves your account.",
            cancellationToken);

    private string BuildLink(string path, string token)
    {
        if (!Uri.TryCreate(_urls.FrontendBaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException(
                "App:FrontendBaseUrl must be an absolute URL to build email links.");
        }

        var root = baseUri.ToString().TrimEnd('/');

        return $"{root}/{path}?token={Uri.EscapeDataString(token)}";
    }

    private string DescribeExpiry(DateTime expiresAtUtc)
    {
        var remaining = expiresAtUtc - _clock.GetUtcNow().UtcDateTime;

        if (remaining.TotalHours >= 47)
        {
            return $"{Math.Round(remaining.TotalDays)} days";
        }

        if (remaining.TotalMinutes >= 90)
        {
            return $"{Math.Round(remaining.TotalHours)} hours";
        }

        return remaining.TotalMinutes >= 55
            ? "1 hour"
            : $"{Math.Max(1, Math.Round(remaining.TotalMinutes))} minutes";
    }

    private Task SendAsync(
        string to,
        string subject,
        string fullName,
        string intro,
        string actionLabel,
        string link,
        DateTime expiresAtUtc,
        string footer,
        CancellationToken cancellationToken)
    {
        var expiry = DescribeExpiry(expiresAtUtc);
        var name = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName.Trim();

        var text =
            $"Hello {name},\n\n{intro}\n\n{actionLabel}: {link}\n\n" +
            $"This link expires in {expiry}.\n{footer}\n\nFASCO Document Clearing LLC";

        var h = (string value) => WebUtility.HtmlEncode(value);

        var html =
            $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#16181D;max-width:560px">
              <p>Hello {h(name)},</p>
              <p>{h(intro)}</p>
              <p style="margin:24px 0">
                <a href="{h(link)}" style="background:#1C1F26;color:#ffffff;padding:10px 18px;border-radius:8px;text-decoration:none;font-weight:bold">{h(actionLabel)}</a>
              </p>
              <p style="color:#6B7280;font-size:12px">This link expires in {h(expiry)}. {h(footer)}</p>
              <p style="color:#6B7280;font-size:12px">If the button does not work, copy this address into your browser:<br>{h(link)}</p>
              <p>FASCO Document Clearing LLC</p>
            </div>
            """;

        return _sender.SendAsync(
            new EmailMessage(to, subject, html, text),
            cancellationToken);
    }
}
