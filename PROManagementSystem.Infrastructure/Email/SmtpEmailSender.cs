using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        var security = !_options.EnableSsl
            ? SecureSocketOptions.None
            : _options.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

        using var client = new SmtpClient
        {
            Timeout = Math.Max(5, _options.TimeoutSeconds) * 1000
        };

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            security,
            cancellationToken);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(
                _options.Username,
                _options.Password ?? string.Empty,
                cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        // Recipient and subject only: the body contains single-use links.
        _logger.LogInformation(
            "Email '{Subject}' sent to {Recipient}.",
            message.Subject,
            message.To);
    }
}
