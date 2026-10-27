namespace PROManagementSystem.Domain.Enums;

/// <summary>How the agency serves a customer.</summary>
public enum CustomerType
{
    /// <summary>Company on a retainer / subscription contract with portal access.</summary>
    Subscription = 1,

    /// <summary>Company served job by job, typically at the counter.</summary>
    WalkInCompany = 2,

    /// <summary>Private individual (sponsor, family, domestic worker employer, visitor).</summary>
    WalkInIndividual = 3
}
