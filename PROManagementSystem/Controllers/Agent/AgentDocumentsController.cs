using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Agent.Commands.AgentConfirmExtraction;
using PROManagementSystem.Application.Agent.Commands.AgentCreateDocumentVersion;
using PROManagementSystem.Application.Agent.Commands.AgentExtractDocument;
using PROManagementSystem.Application.Agent.Commands.AgentRegisterEmployeeDocument;
using PROManagementSystem.Application.Agent.Commands.AgentRegisterEntityDocument;
using PROManagementSystem.Application.Agent.Commands.AgentUpdateDocument;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocumentById;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocumentDependencies;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocumentExtraction;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocumentExtractionFile;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocumentFile;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocumentScan;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocumentVersions;
using PROManagementSystem.Application.Agent.Queries.GetAgentExpiringDocuments;
using PROManagementSystem.Application.Agent.Queries.GetAgentPendingDocumentExtractions;
using PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;
using PROManagementSystem.Application.Documents.Commands.ConfirmDocument;
using PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;
using PROManagementSystem.Application.Documents.Commands.UpdateDocument;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;
using PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;
using PROManagementSystem.Application.Documents.Queries.GetDocumentExtraction;
using PROManagementSystem.Application.Documents.Queries.GetDocumentExtractionFile;
using PROManagementSystem.Application.Documents.Queries.GetDocumentScan;
using PROManagementSystem.Application.Documents.Queries.GetDocumentTypeSchema;
using PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;
using PROManagementSystem.Application.Employees.Commands.CreateEmployeeDocument;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.Agent;

[ApiController]
[Route("api/v1/agent")]
[Authorize(Roles = "Agent")]
public sealed class AgentDocumentsController : ControllerBase
{
    private static readonly HashSet<string> s_allowedPatchFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "DocumentNumber",
            "IssueDate",
            "ExpiryDate",
            "FileUrl",
            "FileName",
            "ContentType",
            "FileSize",
            "Details"
        };

    // The Agent PATCH body is deserialized manually (not via [FromBody] MVC
    // binding), so mirror MVC's case-insensitive property matching here.
    private static readonly System.Text.Json.JsonSerializerOptions
        s_patchDeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly ISender _sender;

    public AgentDocumentsController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // POST /api/v1/agent/documents/extract
    // ================================================================

    [HttpPost("documents/extract")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ExtractDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExtractDocument(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "FILE_REQUIRED",
                    "A document file is required."));
        }

        await using var stream = file.OpenReadStream();

        var command = new AgentExtractDocumentCommand(
            stream,
            file.FileName,
            file.ContentType,
            file.Length);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<ExtractDocumentResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // POST /api/v1/agent/documents/extractions/{extractionId}/confirm
    // ================================================================

    [HttpPost("documents/extractions/{extractionId:guid}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ConfirmDocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConfirmExtraction(
        Guid extractionId,
        [FromBody] ConfirmDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new AgentConfirmExtractionCommand(
            extractionId,
            request.EmployeeId,
            request.ClientEntityId,
            request.Type,
            request.DocumentNumber,
            request.IssueDate,
            request.ExpiryDate,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.FileSize,
            request.Details);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<ConfirmDocumentResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents/{documentId}/file
    // ================================================================

    [HttpGet("documents/{documentId:guid}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentFile(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentDocumentFileQuery(documentId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        // Out-of-portfolio, missing, soft-deleted, no attached file, an
        // unsupported content type or an unreadable/traversal-rejected storage
        // reference are all indistinguishable (404). The storage reference is
        // never returned.
        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "DOCUMENT_FILE_NOT_FOUND",
                        "Document file was not found."));
        }

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    // ================================================================
    // GET /api/v1/agent/documents/extractions
    // ================================================================

    [HttpGet("documents/extractions")]
    [ProducesResponseType(typeof(ApiResponse<GetPendingDocumentExtractionsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPendingDocumentExtractions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAgentPendingDocumentExtractionsQuery(
            page,
            pageSize,
            status);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetPendingDocumentExtractionsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents/extractions/{extractionId}
    // ================================================================

    [HttpGet("documents/extractions/{extractionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentExtractionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentExtraction(
        Guid extractionId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentDocumentExtractionQuery(extractionId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "EXTRACTION_NOT_FOUND",
                        "Document extraction was not found."));
        }

        return Ok(
            ApiResponse<GetDocumentExtractionResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents/extractions/{extractionId}/file
    // ================================================================

    [HttpGet("documents/extractions/{extractionId:guid}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentExtractionFile(
        Guid extractionId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentDocumentExtractionFileQuery(extractionId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "EXTRACTION_NOT_FOUND",
                        "Document extraction was not found."));
        }

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    // ================================================================
    // GET /api/v1/agent/document-types/{documentType}/schema
    // ================================================================

    [HttpGet("document-types/{documentType}/schema")]
    [ProducesResponseType(typeof(ApiResponse<DocumentTypeSchemaResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentTypeSchema(
        string documentType,
        CancellationToken cancellationToken)
    {
        var query = new GetDocumentTypeSchemaQuery(documentType);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<DocumentTypeSchemaResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // POST /api/v1/agent/entities/{entityId}/documents
    // ================================================================

    [HttpPost("entities/{entityId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<CreateClientEntityDocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RegisterEntityDocument(
        Guid entityId,
        [FromBody] CreateClientEntityDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new AgentRegisterEntityDocumentCommand(
            entityId,
            request.Type,
            request.DocumentNumber,
            request.IssueDate,
            request.ExpiryDate,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.FileSize,
            request.Details);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateClientEntityDocumentResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // POST /api/v1/agent/employees/{employeeId}/documents
    // ================================================================

    [HttpPost("employees/{employeeId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<CreateEmployeeDocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RegisterEmployeeDocument(
        Guid employeeId,
        [FromBody] CreateEmployeeDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new AgentRegisterEmployeeDocumentCommand(
            employeeId,
            request.Type,
            request.DocumentNumber,
            request.IssueDate,
            request.ExpiryDate,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.FileSize,
            request.Details);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateEmployeeDocumentResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents
    // ================================================================

    [HttpGet("documents")]
    [ProducesResponseType(typeof(ApiResponse<GetAgentDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] DocumentType? type = null,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] DateTime? expiresBefore = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAgentDocumentsQuery(
            type, status, expiresBefore, page, pageSize);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetAgentDocumentsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents/expiring
    // ================================================================

    [HttpGet("documents/expiring")]
    [ProducesResponseType(typeof(ApiResponse<GetExpiringDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExpiringDocuments(
        [FromQuery] int days = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAgentExpiringDocumentsQuery(
            days, page, pageSize, includeExpired);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetExpiringDocumentsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents/{documentId}
    // ================================================================

    [HttpGet("documents/{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentById(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentDocumentByIdQuery(documentId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "DOCUMENT_NOT_FOUND",
                        "Document was not found."));
        }

        return Ok(
            ApiResponse<GetDocumentByIdResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/agent/documents/{documentId}
    // ================================================================

    [HttpPatch("documents/{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UpdateDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        // Manual JSON property-name validation. The request must not
        // contain any property outside the explicitly allowed Agent
        // fields. Unsupported fields are rejected with a 400 rather
        // than silently ignored.
        JsonDocument jsonDocument;

        try
        {
            jsonDocument = await JsonDocument.ParseAsync(
                Request.Body,
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "INVALID_JSON",
                    "Request body is not valid JSON."));
        }

        using (jsonDocument)
        {
            var root = jsonDocument.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return BadRequest(
                    ApiResponse<object>.FailureResponse(
                        "INVALID_JSON",
                        "Request body must be a JSON object."));
            }

            var unsupportedFields = new List<string>();

            // Track which allowed properties were actually supplied so the
            // PATCH handler can leave omitted properties unchanged.
            var presentProperties =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in root.EnumerateObject())
            {
                if (s_allowedPatchFields.Contains(property.Name))
                {
                    presentProperties.Add(property.Name);
                }
                else
                {
                    unsupportedFields.Add(property.Name);
                }
            }

            if (unsupportedFields.Any())
            {
                return BadRequest(
                    ApiResponse<object>.FailureResponse(
                        "UNSUPPORTED_FIELD",
                        $"Unsupported field(s) for agent document update: " +
                        $"{string.Join(", ", unsupportedFields)}. Allowed fields: " +
                        "DocumentNumber, IssueDate, ExpiryDate, FileUrl, FileName, " +
                        "ContentType, FileSize, Details."));
            }

            // Parse allowed fields via a DTO so values are coerced
            // with standard System.Text.Json semantics.
            AgentUpdateDocumentRequestDto? request;

            try
            {
                request = root.Deserialize<AgentUpdateDocumentRequestDto>(
                    s_patchDeserializeOptions);
            }
            catch (JsonException)
            {
                return BadRequest(
                    ApiResponse<object>.FailureResponse(
                        "INVALID_JSON",
                        "One or more field values are invalid."));
            }

            if (request is null)
            {
                return BadRequest(
                    ApiResponse<object>.FailureResponse(
                        "INVALID_JSON",
                        "Request body is empty."));
            }

            // Validate before calling Trim(): a supplied documentNumber
            // must not be null, empty or whitespace.
            if (presentProperties.Contains("DocumentNumber") &&
                (request.DocumentNumber is null ||
                 string.IsNullOrWhiteSpace(request.DocumentNumber)))
            {
                return BadRequest(
                    ApiResponse<object>.FailureResponse(
                        "INVALID_DOCUMENT_NUMBER",
                        "Document number cannot be null or empty."));
            }

            var command = new AgentUpdateDocumentCommand(
                documentId,
                request.DocumentNumber,
                request.IssueDate,
                request.ExpiryDate,
                request.FileUrl,
                request.FileName,
                request.ContentType,
                request.FileSize,
                request.Details,
                presentProperties);

            var result = await _sender.Send(
                command,
                cancellationToken);

            return Ok(
                ApiResponse<UpdateDocumentResponseDto>
                    .SuccessResponse(result));
        }
    }

    // ================================================================
    // GET /api/v1/agent/documents/{documentId}/scan
    // ================================================================

    [HttpGet("documents/{documentId:guid}/scan")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentScanResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentScan(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentDocumentScanQuery(documentId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetDocumentScanResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // POST /api/v1/agent/documents/{documentId}/versions
    // ================================================================

    [HttpPost("documents/{documentId:guid}/versions")]
    [ProducesResponseType(typeof(ApiResponse<CreateDocumentVersionResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateDocumentVersion(
        Guid documentId,
        [FromBody] CreateDocumentVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new AgentCreateDocumentVersionCommand(
            documentId,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.FileSize);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateDocumentVersionResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents/{documentId}/versions
    // ================================================================

    [HttpGet("documents/{documentId:guid}/versions")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentVersionsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentVersions(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentDocumentVersionsQuery(documentId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetDocumentVersionsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/documents/{documentId}/dependencies
    // ================================================================

    [HttpGet("documents/{documentId:guid}/dependencies")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentDependenciesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentDependencies(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentDocumentDependenciesQuery(documentId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetDocumentDependenciesResponseDto>
                .SuccessResponse(result));
    }
}
