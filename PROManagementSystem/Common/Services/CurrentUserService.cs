using System.Security.Claims;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Api.Common.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }

    public string? Role =>
        _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?
            .User
            .Identity?
            .IsAuthenticated == true;

    public bool IsAdmin =>
        string.Equals(
            Role,
            "Admin",
            StringComparison.OrdinalIgnoreCase);

    public Guid? ClientCompanyId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue("client_company_id");

            return Guid.TryParse(value, out var clientCompanyId)
                ? clientCompanyId
                : null;
        }
    }
}