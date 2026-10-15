using PROMangementSystem.Domain.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Domain.Entities;

public sealed class ServiceContract : BaseEntity
{
    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public string ContractNumber { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public ServiceContractStatus Status { get; set; }
        = ServiceContractStatus.Active;

    public decimal RetainerAmount { get; set; }

    public string? Terms { get; set; }
}