using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>
/// A single-use, expiring token that lets its holder set the account
/// password. Only the SHA-256 hash of the token is stored.
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public AccountTokenPurpose Purpose { get; set; }
        = AccountTokenPurpose.PasswordReset;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}
