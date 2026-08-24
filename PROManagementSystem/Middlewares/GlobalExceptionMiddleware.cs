using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Exceptions;

namespace PROManagementSystem.Api.Common.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await HandleExceptionAsync(context, exception);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var code = "INTERNAL_SERVER_ERROR";
        var message = "An unexpected error occurred.";
        object? details = null;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = HttpStatusCode.BadRequest;
                code = "VALIDATION_ERROR";
                message = "One or more validation errors occurred.";

                details = validationException.Errors
                    .GroupBy(x => x.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(x => x.ErrorMessage)
                            .ToArray());

                break;

            case ForbiddenAccessException:
                statusCode = HttpStatusCode.Forbidden;
                code = "FORBIDDEN";
                message = exception.Message;
                break;

            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                code = "UNAUTHORIZED";
                message = exception.Message;
                break;

            case ConflictException:
                statusCode = HttpStatusCode.Conflict;
                code = "CONFLICT";
                message = exception.Message;
                break;

            case NotFoundException:
                statusCode = HttpStatusCode.NotFound;
                code = "NOT_FOUND";
                message = exception.Message;
                break;

            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                code = "NOT_FOUND";
                message = exception.Message;
                break;

            case ArgumentException:
                statusCode = HttpStatusCode.BadRequest;
                code = "BAD_REQUEST";
                message = exception.Message;
                break;

            case InvalidOperationException:
                statusCode = HttpStatusCode.Conflict;
                code = "CONFLICT";
                message = exception.Message;
                break;
        }

        var response = ApiResponse<object>.FailureResponse(
            code,
            message,
            details);

        response.StatusCode = (int)statusCode;

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, s_jsonOptions));
    }
}