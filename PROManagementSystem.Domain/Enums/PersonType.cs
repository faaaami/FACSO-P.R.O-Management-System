namespace PROManagementSystem.Domain.Enums;

/// <summary>The role a person plays for the customer they are registered under.</summary>
public enum PersonType
{
    Employee = 1,
    PartnerInvestor = 2,
    Dependent = 3,
    DomesticWorker = 4,

    /// <summary>The walk-in individual customer themselves.</summary>
    Individual = 5
}
