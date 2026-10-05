using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace PROManagementSystem.Tests.RateLimiting;

/// <summary>
/// Test-only middleware that lets a request declare the client IP the auth
/// rate limiter partitions on. TestServer requests share a single connection,
/// so without this override every request would fall into the same partition
/// and the partition-isolation guarantees could not be exercised.
/// </summary>
internal sealed class RemoteIpHeaderStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(
        Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (context, following) =>
            {
                if (context.Request.Headers.TryGetValue(
                        HeaderName,
                        out var value) &&
                    IPAddress.TryParse(
                        value.ToString(),
                        out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                await following();
            });

            next(app);
        };
}
