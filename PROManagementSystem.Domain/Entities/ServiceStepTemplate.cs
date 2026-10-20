using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>A step copied onto every new job of the parent service.</summary>
public sealed class ServiceStepTemplate : BaseEntity
{
    public Guid ServiceDefinitionId { get; set; }

    public ServiceDefinition? ServiceDefinition { get; set; }

    public int Order { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
