using MediatR;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Agent.Commands.AgentExtractDocument;

public sealed class AgentExtractDocumentCommandHandler
    : IRequestHandler<
        AgentExtractDocumentCommand,
        ExtractDocumentResponseDto>
{
    private readonly IDocumentExtractionService _extractor;
    private readonly IFileStorageService _storage;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AgentExtractDocumentCommandHandler(
        IDocumentExtractionService extractor,
        IFileStorageService storage,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _extractor = extractor;
        _storage = storage;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ExtractDocumentResponseDto> Handle(
        AgentExtractDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileStream is null)
        {
            throw new ArgumentNullException(
                nameof(request.FileStream));
        }

        if (request.FileSize <= 0)
        {
            throw new InvalidOperationException(
                "Uploaded file is empty.");
        }

        // Ensure the caller is an authenticated agent.
        var agentId = _currentUser.UserId!.Value;

        // 1. Persist the validated upload under a server-generated
        //    storage reference before anything else can succeed.
        var storedReference =
            await _storage.SaveAsync(
                request.FileStream,
                request.FileName,
                request.ContentType,
                cancellationToken);

        try
        {
            // Reset the stream so the extraction service reads from
            // the start (storage has consumed it to EOF).
            if (request.FileStream.CanSeek)
            {
                request.FileStream.Position = 0;
            }

            var extractionResult =
                await _extractor.ExtractAsync(
                    request.FileStream,
                    request.ContentType,
                    cancellationToken);

            var now = DateTime.UtcNow;

            var extraction = new DocumentExtraction
            {
                Id = Guid.NewGuid(),

                FileName = request.FileName,

                FileUrl = storedReference,

                ContentType = request.ContentType,

                FileSize = request.FileSize,

                SuggestedType = extractionResult.Type,

                SuggestedDocumentNumber =
                    extractionResult.DocumentNumber,

                SuggestedIssueDate =
                    extractionResult.IssueDate,

                SuggestedExpiryDate =
                    extractionResult.ExpiryDate,

                SuggestedOwnerType =
                    extractionResult.OwnerType,

                // Task 7B.5: type-specific field suggestions from the
                // deterministic extractor; empty/null persists the canonical
                // "{}". Never authoritative, never copied to Document.Details.
                SuggestedDetails =
                    DetailsJson.ToStorage(extractionResult.Details),

                Confidence = extractionResult.Confidence,

                Status = "Pending",

                CreatedBy = agentId,

                CreatedAt = now,

                UpdatedAt = now,

                IsDeleted = false,

                DeletedAt = null,

                ExpiresAt = now.AddHours(24)
            };

            _context.DocumentExtractions.Add(extraction);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = agentId,
                Action = "DocumentExtractionStarted",
                EntityType = "DocumentExtraction",
                EntityId = extraction.Id,
                Description =
                    $"Document extraction started for file '{request.FileName}'.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync(
                cancellationToken);

            return new ExtractDocumentResponseDto(
                extraction.Id,
                extraction.SuggestedType,
                extraction.SuggestedDocumentNumber,
                extraction.SuggestedIssueDate,
                extraction.SuggestedExpiryDate,
                extraction.SuggestedOwnerType,
                extraction.Confidence,
                DetailsJson.FromStorage(extraction.SuggestedDetails));
        }
        catch
        {
            // Best-effort cleanup so a failed request does not leave an
            // orphaned file behind a missing extraction record.
            await _storage.DeleteAsync(
                storedReference,
                CancellationToken.None);

            throw;
        }
    }
}
