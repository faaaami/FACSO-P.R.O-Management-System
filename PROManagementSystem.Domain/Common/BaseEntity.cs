

using PROManagementSystem.Application.Common.Interfaces;

namespace PROMangementSystem.Domain.Common;


public abstract class BaseEntity : ISoftDeletable
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}