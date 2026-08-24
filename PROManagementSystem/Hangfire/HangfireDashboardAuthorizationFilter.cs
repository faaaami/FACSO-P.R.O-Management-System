using Hangfire.Dashboard;

namespace PROManagementSystem.Api.Hangfire;

public sealed class HangfireDashboardAuthorizationFilter
    : IDashboardAuthorizationFilter
{
    public bool Authorize(
        DashboardContext context)
    {
        var httpContext =
            context.GetHttpContext();

        if (!httpContext.User.Identity
            ?.IsAuthenticated ?? false)
        {
            return false;
        }

        var isAdmin =
            httpContext.User
                .IsInRole("Admin");

        return isAdmin;
    }
}
