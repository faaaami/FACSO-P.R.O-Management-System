using System.Security.Claims;
using Serilog.Context;

namespace PROManagementSystem.Api.Common.Middlewares;

public sealed class UserContextLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        var role = context.User.FindFirst(
            ClaimTypes.Role)?.Value;

        using (LogContext.PushProperty(
                   "RequestId",
                   context.TraceIdentifier))
        using (LogContext.PushProperty(
                   "UserId",
                   userId ?? "Anonymous"))
        using (LogContext.PushProperty(
                   "Role",
                   role ?? "Anonymous"))
        {
            await _next(context);
        }
    }
}