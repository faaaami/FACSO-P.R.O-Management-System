using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Email;
using PROManagementSystem.Tests.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Security;

/// <summary>
/// Regression coverage for audit finding H1: account tokens (verification,
/// password reset, invitation) must never be written to the application log,
/// while the emails themselves must carry them as working links.
/// </summary>
public sealed class EmailSecretLoggingTests
{
    private const string VerificationToken = "SENTINEL-VERIFICATION-TOKEN-8f3a1c";
    private const string ResetToken = "SENTINEL-RESET-TOKEN-1b2c9d";
    private const string InvitationToken = "SENTINEL-INVITATION-TOKEN-4e5f6a";
    private const string Recipient = "client@example.test";
    private const string FrontendBaseUrl = "https://portal.example.test/";

    private static EmailService NewEmailService(IEmailSender sender)
        => new(
            sender,
            Options.Create(new AppUrlOptions { FrontendBaseUrl = FrontendBaseUrl }),
            TimeProvider.System);

    private static async Task SendAllAsync(IEmailService service)
    {
        var expires = DateTime.UtcNow.AddHours(1);

        await service.SendEmailVerificationAsync(
            Recipient, "Invited User", VerificationToken, expires);
        await service.SendPasswordResetAsync(
            Recipient, "Invited User", ResetToken, expires);
        await service.SendClientInvitationAsync(
            Recipient, "Invited User", "Company A", InvitationToken, expires);
    }

    [Fact(DisplayName =
        "H1: the Development log-only sender never writes a token to the log")]
    public async Task LoggingSender_NeverLogsTokens()
    {
        var logger = new CapturingLogger<LoggingEmailSender>();

        await SendAllAsync(NewEmailService(new LoggingEmailSender(logger)));

        Assert.Equal(3, logger.Entries.Count);
        Assert.All(logger.Entries, entry =>
        {
            Assert.Contains(Recipient, entry, StringComparison.Ordinal);
            Assert.DoesNotContain(VerificationToken, entry, StringComparison.Ordinal);
            Assert.DoesNotContain(ResetToken, entry, StringComparison.Ordinal);
            Assert.DoesNotContain(InvitationToken, entry, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName =
        "Emails carry each token in a link to the matching frontend page")]
    public async Task Emails_LinkToTheFrontendWithTheToken()
    {
        var sender = new CapturingEmailSender();

        await SendAllAsync(NewEmailService(sender));

        Assert.Equal(
            VerificationToken,
            sender.LatestToken(Recipient, "verify-email"));
        Assert.Equal(
            ResetToken,
            sender.LatestToken(Recipient, "reset-password"));
        Assert.Equal(
            InvitationToken,
            sender.LatestToken(Recipient, "accept-invitation"));

        Assert.All(sender.Messages, message =>
        {
            Assert.Contains("https://portal.example.test/", message.TextBody);
            Assert.DoesNotContain("//reset", message.TextBody);
        });
    }

    [Fact(DisplayName = "Email names are HTML-encoded in the HTML body")]
    public async Task Html_IsEncoded()
    {
        var sender = new CapturingEmailSender();

        await NewEmailService(sender).SendClientInvitationAsync(
            Recipient,
            "<script>alert(1)</script>",
            "A & B Trading",
            InvitationToken,
            DateTime.UtcNow.AddHours(72));

        var html = Assert.Single(sender.Messages).HtmlBody;

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("A &amp; B Trading", html);
    }
}

public sealed class EmailSenderSelectionTests
{
    private static readonly SmtpOptions Unconfigured = new();

    private static readonly SmtpOptions Configured = new()
    {
        Host = "smtp.example.test",
        FromAddress = "noreply@example.test"
    };

    [Fact(DisplayName =
        "Development without SMTP uses the log-only sender")]
    public void Development_WithoutSmtp_UsesLoggingSender()
        => Assert.Equal(
            typeof(LoggingEmailSender),
            EmailSenderSelection.Select(isDevelopment: true, Unconfigured));

    [Theory(DisplayName = "Configured SMTP is used in every environment")]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfiguredSmtp_IsUsed(bool isDevelopment)
        => Assert.Equal(
            typeof(SmtpEmailSender),
            EmailSenderSelection.Select(isDevelopment, Configured));

    [Fact(DisplayName =
        "Outside Development, missing SMTP fails instead of silently logging")]
    public void Production_WithoutSmtp_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => EmailSenderSelection.Select(isDevelopment: false, Unconfigured));
}
