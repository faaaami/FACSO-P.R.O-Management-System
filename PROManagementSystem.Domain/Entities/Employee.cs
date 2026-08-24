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

    public ICollection<Document> Documents { get; set; }
    = new List<Document>();
}