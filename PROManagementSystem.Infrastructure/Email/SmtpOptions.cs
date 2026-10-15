namespace PROManagementSystem.Infrastructure.Email;

/// <summary>SMTP transport settings ("Smtp" section).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>Optional: leave empty for relays without authentication.</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "FASCO PRO Services";

    /// <summary>
    /// Use TLS: implicit TLS on port 465, otherwise STARTTLS (required).
    /// Set false only for a local test server such as Mailpit.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}

/// <summary>Public URLs used in emails ("App" section).</summary>
public sealed class AppUrlOptions
{
    public const string SectionName = "App";

    /// <summary>Base URL of the web frontend, e.g. https://pro.fasco.ae</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;
}
