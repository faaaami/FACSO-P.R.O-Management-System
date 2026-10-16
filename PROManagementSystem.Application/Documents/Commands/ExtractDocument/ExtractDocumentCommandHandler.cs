using MediatR;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

public sealed class ExtractDocumentCommandHandler
    : IRequestHandler<
        ExtractDocumentCommand,
        ExtractDocumentResponseDto>
{
    private readonly IDocumentExtractionService _extractor;
    private readonly IFileStorageService _storage;
    private readonly IUploadImageConverter _imageConverter;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ExtractDocumentCommandHandler(
        IDocumentExtractionService extractor,
        IFileStorageService storage,
        IUploadImageConverter imageConverter,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _extractor = extractor;
        _storage = storage;
        _imageConverter = imageConverter;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ExtractDocumentResponseDto> Handle(
        ExtractDocumentCommand request,
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

        // HEIC/HEIF photos are stored (and read) as JPEG so the browser can
        // preview them; other files are stored exactly as uploaded.
        await using var converted =
            await _imageConverter.ConvertForStorageAsync(
                request.FileStream,
                request.FileName,
                request.ContentType,
                cancellationToken);

        var fileStream = converted?.Content ?? request.FileStream;
        var fileName = converted?.FileName ?? request.FileName;
        var contentType = converted?.ContentType ?? request.ContentType;
        var fileSize = converted?.FileSize ?? request.FileSize;

        // 1. Persist the validated upload under a server-generated
        //    storage reference before anything else can succeed.
        var storedReference =
            await _storage.SaveAsync(
                fileStream,
                fileName,
                contentType,
                cancellationToken);

        try
        {
            // Reset the stream so the extraction service reads from
            // the start (storage has consumed it to EOF).
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            // 2. Send document to extraction service
            var extractionResult =
                await _extractor.ExtractAsync(
                    fileStream,
                    contentType,
                    cancellationToken);

            // 3. Create AI extraction draft
            var now = DateTime.UtcNow;

            var extraction = new DocumentExtraction
            {
                Id = Guid.NewGuid(),

                FileName =
                    fileName,

                FileUrl =
                    storedReference,

                ContentType =
                    contentType,

                FileSize =
                    fileSize,

                SuggestedType =
                    extractionResult.Type,

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

                Confidence =
                    extractionResult.Confidence,

                Status =
                    "Pending",

                CreatedAt =
                    now,

                UpdatedAt =
                    now,

                IsDeleted =
                    false,

                DeletedAt =
                    null,

                ExpiresAt =
                    now.AddHours(24)
            };

            _context.DocumentExtractions.Add(
                extraction);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "DocumentExtractionStarted",
                EntityType = "DocumentExtraction",
                EntityId = extraction.Id,
                Description =
                    $"Document extraction started for file '{fileName}'.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync(
                cancellationToken);

            // 4. Return draft
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