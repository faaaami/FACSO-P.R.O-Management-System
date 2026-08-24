using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; } = false;

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiry { get; set; }

    public bool EmailVerified { get; set; } = false;

    public Guid? ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }
}