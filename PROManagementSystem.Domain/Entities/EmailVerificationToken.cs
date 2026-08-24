using PROManagementSystem.Domain.Entities;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class EmailVerificationToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}