using PROMangementSystem.Domain.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Domain.Entities;

public class RenewalTaskHistory : BaseEntity
{
    public Guid RenewalTaskId { get; set; }

    public RenewalTask? RenewalTask { get; set; }

    public RenewalTaskStatus Status { get; set; }

    public Guid ChangedBy { get; set; }

    public User? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Note { get; set; }
}