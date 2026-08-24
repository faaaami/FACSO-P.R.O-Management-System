using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public sealed class ClientCompany : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;

    public string? TradeLicenseNumber { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Emirate { get; set; }

    public bool IsActive { get; set; } = true;

    // Relationships

    public ICollection<ClientEntity> Entities { get; set; }
        = new List<ClientEntity>();

    public ICollection<User> Contacts { get; set; }
        = new List<User>();
}