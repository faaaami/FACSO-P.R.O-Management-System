using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Records outgoing email instead of sending it, so tests can follow the
/// links a user would click (invitation, password reset, verification).
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => _messages.ToArray();

    /// <summary>When set, every send throws (to test delivery failures).</summary>
    public bool FailSends { get; set; }

    public Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        if (FailSends)
        {
            throw new InvalidOperationException("Simulated SMTP failure.");
        }

        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _messages.Clear();
        FailSends = false;
    }

    public IReadOnlyList<EmailMessage> To(string recipient)
        => Messages
            .Where(x => string.Equals(x.To, recipient, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// The token from the most recent email to <paramref name="recipient"/>
    /// whose link targets <paramref name="frontendPath"/> (for example
    /// "accept-invitation"), or null.
    /// </summary>
    public string? LatestToken(string recipient, string frontendPath)
    {
        var pattern = new Regex(
            "/" + Regex.Escape(frontendPath) + @"\?token=([^\s""<&]+)");

        return To(recipient)
            .Select(x => pattern.Match(x.TextBody))
            .Where(match => match.Success)
            .Select(match => Uri.UnescapeDataString(match.Groups[1].Value))
            .LastOrDefault();
    }
}
