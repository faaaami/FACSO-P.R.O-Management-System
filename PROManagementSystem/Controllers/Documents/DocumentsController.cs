using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityDocuments;
using PROManagementSystem.Application.Documents.Commands.ConfirmDocument;
using PROManagementSystem.Application.Documents.Commands.CreateDocumentDependency;
using PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;
using PROManagementSystem.Application.Documents.Commands.DeleteDocument;
using PROManagementSystem.Application.Documents.Commands.DeleteDocumentDependency;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;
using PROManagementSystem.Application.Documents.Commands.UpdateDocument;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;
using PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;
using PROManagementSystem.Application.Documents.Queries.GetDocumentScan;
using PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Api.Controllers.Documents;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class DocumentsController : ControllerBase
{
    private readonly ISender _sender;

    public DocumentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("entities/{entityId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<CreateClientEntityDocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateEntityDocument(
        Guid entityId,
        [FromBody] CreateClientEntityDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new CreateClientEntityDocumentCommand(
            entityId,
            request.Type,
            request.DocumentNumber,
            request.IssueDate,
            request.ExpiryDate,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.FileSize);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateClientEntityDocumentResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("entities/{entityId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<GetClientEntityDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEntityDocuments(
    Guid entityId,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetClientEntityDocumentsQuery(
            entityId,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetClientEntityDocumentsResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("documents/{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
    Guid documentId,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetDocumentByIdQuery(
            documentId,
            includeDeleted);

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

    [HttpPatch("documents/{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UpdateDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
    Guid documentId,
    [FromBody] UpdateDocumentRequestDto request,
    CancellationToken cancellationToken)
    {
        var command = new UpdateDocumentCommand(
            documentId,
            request.Type,
            request.DocumentNumber,
            request.IssueDate,
            request.ExpiryDate,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.FileSize,
            request.IsActive);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateDocumentResponseDto>
                .SuccessResponse(result));
    }

    [HttpDelete("documents/{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
    Guid documentId,
    CancellationToken cancellationToken)
    {
        var command = new DeleteDocumentCommand(
            documentId);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<DeleteDocumentResponseDto>
                .SuccessResponse(result));
    }

    [HttpPost("documents/{documentId:guid}/versions")]
    [ProducesResponseType(typeof(ApiResponse<CreateDocumentVersionResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateVersion(
    Guid documentId,
    [FromBody] CreateDocumentVersionRequestDto request,
    CancellationToken cancellationToken)
    {
        var command = new CreateDocumentVersionCommand(
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

    [HttpGet("documents/{documentId:guid}/versions")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentVersionsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVersions(
    Guid documentId,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetDocumentVersionsQuery(
            documentId,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetDocumentVersionsResponseDto>
                .SuccessResponse(result));
    }


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
        var query = new GetExpiringDocumentsQuery(
            days,
            page,
            pageSize,
            includeExpired);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetExpiringDocumentsResponseDto>
                .SuccessResponse(result));
    }

    [HttpPost("documents/{documentId:guid}/dependencies")]
    [ProducesResponseType(typeof(ApiResponse<CreateDocumentDependencyResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateDependency(
    Guid documentId,
    [FromBody] CreateDocumentDependencyRequest request,
    CancellationToken cancellationToken)
    {
        var command = new CreateDocumentDependencyCommand(
            documentId,
            request.DependsOnDocumentId);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateDocumentDependencyResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("documents/{documentId:guid}/dependencies")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentDependenciesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDependencies(
    Guid documentId,
    CancellationToken cancellationToken)
    {
        var query = new GetDocumentDependenciesQuery(
            documentId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetDocumentDependenciesResponseDto>
                .SuccessResponse(result));
    }

    [HttpDelete(
    "documents/{documentId:guid}/dependencies/{dependencyId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteDocumentDependencyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDependency(
    Guid documentId,
    Guid dependencyId,
    CancellationToken cancellationToken)
    {
        var command = new DeleteDocumentDependencyCommand(
            documentId,
            dependencyId);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<DeleteDocumentDependencyResponseDto>
                .SuccessResponse(result));
    }


    //[HttpGet("documents/{documentId:guid}/scan")]
    ////public async Task<IActionResult> GetScan(
    ////Guid documentId,
    ////CancellationToken cancellationToken)
    ////{
    ////    var query = new GetDocumentScanQuery(
    ////        documentId);

    ////    var result = await _sender.Send(
    ////        query,
    ////        cancellationToken);

    ////    return Redirect(result.FileUrl);
    ////}
    ///

    [HttpGet("documents/{documentId:guid}/scan")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentScanResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetScan(
    Guid documentId,
    CancellationToken cancellationToken)
    {
        var query = new GetDocumentScanQuery(
            documentId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetDocumentScanResponseDto>
                .SuccessResponse(result));
    }

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

        await using var stream =
            file.OpenReadStream();

        var command = new ExtractDocumentCommand(
            stream,
            file.FileName,
            file.ContentType,
            file.Length);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<ExtractDocumentResponseDto>
                .SuccessResponse(result));
    }

    [HttpPost("documents/extractions/{extractionId:guid}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ConfirmDocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConfirmDocument(
    Guid extractionId,
    [FromBody] ConfirmDocumentRequestDto request,
    CancellationToken cancellationToken)
    {
        var command = new ConfirmDocumentCommand(
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
            request.FileSize);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<ConfirmDocumentResponseDto>
                .SuccessResponse(result));
    }
}
