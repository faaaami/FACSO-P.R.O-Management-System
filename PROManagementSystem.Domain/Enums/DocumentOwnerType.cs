namespace PROManagementSystem.Domain.Enums;

/// <summary>
/// Filters documents by the type of resource that owns them (exactly one of
/// ClientEntityId or EmployeeId is set per document).
/// </summary>
public enum DocumentOwnerType
{
    Entity = 1,
    Employee = 2
}