using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>
/// Subscription pricing: the service fee a contracted customer pays for one
/// service, or that the service is covered by the retainer.
/// </summary>
public sealed class ContractServicePrice : BaseEntity
{
    public Guid ServiceContractId { get; set; }

    public ServiceContract? ServiceContract { get; set; }

    public Guid ServiceDefinitionId { get; set; }

    public ServiceDefinition? ServiceDefinition { get; set; }

    public decimal ServiceFee { get; set; }

    /// <summary>When true the service fee is covered by the retainer (charged as 0).</summary>
    public bool IncludedInRetainer { get; set; }
}
