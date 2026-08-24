using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PROManagementSystem.Api.Common;

public sealed class ApiResponseFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult
            && objectResult.Value is IApiResponse response)
        {
            response.StatusCode = objectResult.StatusCode
                ?? StatusCodes.Status200OK;
        }

        await next();
    }
}
