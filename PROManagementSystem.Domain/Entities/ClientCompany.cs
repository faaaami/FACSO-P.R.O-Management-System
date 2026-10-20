using PROManagementSystem.Domain.Enums;
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

    public CustomerType CustomerType { get; set; } = CustomerType.Subscription;

    /// <summary>Short sequential code for counter lookup, e.g. C-000123.</summary>
    public string? CustomerCode { get; set; }

    /// <summary>UAE VAT Tax Registration Number (15 digits), when registered.</summary>
    public string? TaxRegistrationNumber { get; set; }

    public string? ContactPersonName { get; set; }

    public string? Notes { get; set; }

    // Relationships

    public ICollection<ClientEntity> Entities { get; set; }
        = new List<ClientEntity>();

    public ICollection<User> Contacts { get; set; }
        = new List<User>();
}