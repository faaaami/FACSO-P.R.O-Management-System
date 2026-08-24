using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public User User { get; set; } = null!;
}