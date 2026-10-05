using System.Net;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7C.3.1: the internal raw-response boundary between the OpenRouter
/// HTTP transport and the structured-response parser (Task 7C.3.2). Contains
/// only the HTTP status and the raw response JSON — nothing more. It is never
/// serialised, persisted, or surfaced through the API, and the raw JSON is
/// never logged.
/// </summary>
internal sealed record OpenRouterRawResponse(
    HttpStatusCode StatusCode,
    bool IsSuccessStatusCode,
    string? JsonContent);
