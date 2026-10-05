using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public sealed class ServiceRequest : BaseEntity
{
    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public ServiceRequestType Type { get; set; }
        = ServiceRequestType.Other;

    public Guid? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public Guid? EntityId { get; set; }

    public ClientEntity? Entity { get; set; }

    public Guid? DocumentId { get; set; }

    public Document? Document { get; set; }

    public ServiceRequestStatus Status { get; set; }
        = ServiceRequestStatus.Submitted;

    public string? Description { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? ConvertedAt { get; set; }

    public DateTime? RejectedAt { get; set; }

    public Guid? ConvertedRenewalTaskId { get; set; }

    public RenewalTask? ConvertedRenewalTask { get; set; }
}