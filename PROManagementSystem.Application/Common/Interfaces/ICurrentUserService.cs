namespace PROManagementSystem.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? Role { get; }

    bool IsAuthenticated { get; }

    bool IsAdmin { get; }

    Guid? ClientCompanyId { get; }
}