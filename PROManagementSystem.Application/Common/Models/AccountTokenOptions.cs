namespace PROManagementSystem.Application.Common.Models;

/// <summary>
/// Lifetimes of emailed account tokens ("Auth:Tokens"). Password-reset links
/// are fixed at one hour (<see cref="Helpers.AccountTokens.PasswordResetLifetime"/>).
/// </summary>
public sealed class AccountTokenOptions
{
    public const string SectionName = "Auth:Tokens";

    public int InvitationLifetimeHours { get; set; } = 72;

    public int EmailVerificationLifetimeHours { get; set; } = 24;
}

/// <summary>Accounts the system owns and that no person may sign in to.</summary>
public sealed class SystemAccountOptions
{
    public Guid AutomationUserId { get; set; }
}
