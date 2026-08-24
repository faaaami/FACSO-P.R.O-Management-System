using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class ClientEntity : BaseEntity
{
    public Guid ClientCompanyId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string? TradeLicenseNumber { get; set; }

    public string? Emirate { get; set; }

    public bool IsActive { get; set; } = true;

    public ClientCompany ClientCompany { get; set; } = null!;

    // Employees belonging to this entity
    public ICollection<Employee> Employees { get; set; }
        = new List<Employee>();

    // Documents belonging to this entity
    public ICollection<Document> Documents { get; set; }
        = new List<Document>();
}