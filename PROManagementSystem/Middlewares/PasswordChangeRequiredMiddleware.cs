using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Api.Common.Middlewares;

/// <summary>
/// While the signed-in user's token carries the password-change claim, only
/// the endpoints needed to change the password (and to read one's own
/// profile or sign out) are allowed. Everything else returns 403
/// PASSWORD_CHANGE_REQUIRED. After changing the password the client refreshes
/// its token, which no longer carries the claim.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware
{
    public const string ErrorCode = "PASSWORD_CHANGE_REQUIRED";

    private static readonly string[] AllowedPaths =
    {
        "/api/v1/auth/",
        "/api/v1/users/me",
        "/api/v1/users/change-password",
        "/api/v1/client/me",
        "/api/v1/agent/me",
        "/api/v1/health"
    };

    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var mustChange =
            context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(
                ITokenService.PasswordChangeRequiredClaim,
                "true");

        if (!mustChange || IsAllowed(context.Request.Path))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var payload = ApiResponse<object>.FailureResponse(
            ErrorCode,
            "You must change your password before continuing.");

        payload.StatusCode = StatusCodes.Status403Forbidden;

        await context.Response.WriteAsJsonAsync(payload);
    }

    private static bool IsAllowed(PathString path)
    {
        var value = (path.Value ?? string.Empty).TrimEnd('/');

        // A prefix matches itself and its sub-paths, so "/api/v1/client/me"
        // also covers "/api/v1/client/me/change-password".
        return AllowedPaths.Any(allowed =>
        {
            var prefix = allowed.TrimEnd('/');

            return value.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        });
    }
}
