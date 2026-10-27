using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>A document the customer must provide for the parent service.</summary>
public sealed class ServiceRequiredDocument : BaseEntity
{
    public Guid ServiceDefinitionId { get; set; }

    public ServiceDefinition? ServiceDefinition { get; set; }

    public int Order { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsMandatory { get; set; } = true;
}
