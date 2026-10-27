using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class Employee : BaseEntity
{
    public Guid ClientEntityId { get; set; }

    public ClientEntity ClientEntity { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;

    public string? PassportNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Nationality { get; set; }

    public string? JobTitle { get; set; }

    public DateTime HireDate { get; set; }

    public bool IsActive { get; set; } = true;

    public PersonType PersonType { get; set; } = PersonType.Employee;

    public string? FullNameArabic { get; set; }

    /// <summary>"M" or "F".</summary>
    public string? Gender { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    /// <summary>784-YYYY-NNNNNNN-N.</summary>
    public string? EmiratesIdNumber { get; set; }

    /// <summary>ICP unified number (UID).</summary>
    public string? UidNumber { get; set; }

    public string? MohrePersonCode { get; set; }

    /// <summary>For dependents and domestic workers: the sponsoring person.</summary>
    public Guid? SponsorId { get; set; }

    public Employee? Sponsor { get; set; }

    /// <summary>Relationship to the sponsor, e.g. Spouse, Son, Daughter, Parent.</summary>
    public string? RelationshipToSponsor { get; set; }

    public ICollection<Document> Documents { get; set; }
    = new List<Document>();
}