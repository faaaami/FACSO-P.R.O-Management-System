namespace PROManagementSystem.Domain.Enums;

/// <summary>Who a service is performed for.</summary>
public enum ServiceSubjectType
{
    /// <summary>A person (employee, dependent, domestic worker, individual).</summary>
    Person = 1,

    /// <summary>A company / licence (client entity).</summary>
    Company = 2,

    /// <summary>No specific subject (e.g. a typing or translation job).</summary>
    None = 3
}
