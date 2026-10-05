using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

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
    private readonly IDuplicateDocumentTranslator _duplicateTranslator;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IDuplicateDocumentTranslator duplicateTranslator)
    {
        _next = next;
        _logger = logger;
        _duplicateTranslator = duplicateTranslator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            // Task 7B.6.4: a database-level unique violation on the document
            // constraint (raised when a concurrent confirmation or a direct
            // insert races past the application guard) is surfaced as the same
            // 409 DUPLICATE_DOCUMENT the guard produces. Unrelated unique
            // violations pass through untouched.
            if (exception is DbUpdateException dbUpdateException)
            {
                exception =
                    _duplicateTranslator.Translate(dbUpdateException) ??
                    exception;
            }

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

            case DuplicateDocumentException:
                statusCode = HttpStatusCode.Conflict;
                code = "DUPLICATE_DOCUMENT";
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