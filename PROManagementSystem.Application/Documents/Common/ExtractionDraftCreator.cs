using System.Globalization;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Common;

/// <summary>The validated upload for a new extraction draft.</summary>
public sealed record ExtractionUpload(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize,
    DocumentType DocumentType);

/// <summary>
/// Shared by the Admin and Agent extract commands: converts HEIC to JPEG,
/// stores the file, reads suggestions for the chosen document type and saves
/// a Pending draft that expires after 24 hours. Nothing is written to the
/// document registry; staff confirm the draft separately.
/// </summary>
public sealed class ExtractionDraftCreator
{
    private readonly IDocumentExtractionService _extractor;
    private readonly IFileStorageService _storage;
    private readonly IUploadImageConverter _imageConverter;
    private readonly IDocumentTypeSchemaProvider _schemas;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ExtractionDraftCreator(
        IDocumentExtractionService extractor,
        IFileStorageService storage,
        IUploadImageConverter imageConverter,
        IDocumentTypeSchemaProvider schemas,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _extractor = extractor;
        _storage = storage;
        _imageConverter = imageConverter;
        _schemas = schemas;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ExtractDocumentResponseDto> CreateAsync(
        ExtractionUpload upload,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        if (upload.FileStream is null)
        {
            throw new ArgumentNullException(nameof(upload.FileStream));
        }

        if (upload.FileSize <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        // HEIC/HEIF photos are stored (and read) as JPEG so the browser can
        // preview them; other files are stored exactly as uploaded.
        await using var converted = await _imageConverter.ConvertForStorageAsync(
            upload.FileStream,
            upload.FileName,
            upload.ContentType,
            cancellationToken);

        var fileStream = converted?.Content ?? upload.FileStream;
        var fileName = converted?.FileName ?? upload.FileName;
        var contentType = converted?.ContentType ?? upload.ContentType;
        var fileSize = converted?.FileSize ?? upload.FileSize;

        // 1. Persist the validated upload under a server-generated storage
        //    reference before anything else can succeed.
        var storedReference = await _storage.SaveAsync(
            fileStream,
            fileName,
            contentType,
            cancellationToken);

        try
        {
            // Storage has read the stream to the end.
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            // 2. Read suggestions for the type staff selected.
            var result = await _extractor.ExtractAsync(
                new DocumentExtractionRequest(fileStream, contentType, upload.DocumentType),
                cancellationToken);

            // 3. Save the draft.
            var now = DateTime.UtcNow;
            var details = SchemaDetails(upload.DocumentType, result.Fields);

            var extraction = new DocumentExtraction
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                FileUrl = storedReference,
                ContentType = contentType,
                FileSize = fileSize,
                SuggestedType = upload.DocumentType,
                SuggestedDocumentNumber = Value(result.Fields, DocumentExtractionResult.DocumentNumberKey),
                SuggestedIssueDate = Date(result.Fields, DocumentExtractionResult.IssueDateKey),
                SuggestedExpiryDate = Date(result.Fields, DocumentExtractionResult.ExpiryDateKey),
                SuggestedOwnerType = null,
                SuggestedDetails = DetailsJson.ToStorage(details),
                FieldResults = ExtractionFieldResults.ToStorage(result.Fields),
                FailureCode = result.Failure?.Code,
                FailureMessage = result.Failure?.Message,
                Status = "Pending",
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = now.AddHours(24)
            };

            _context.DocumentExtractions.Add(extraction);

            // No file name or extracted value in the audit trail: both can
            // contain personal data.
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "DocumentExtractionStarted",
                EntityType = "DocumentExtraction",
                EntityId = extraction.Id,
                Description =
                    $"Document extraction started ({upload.DocumentType}, " +
                    $"{(result.Failure is null ? "read" : result.Failure.Code)}).",
                CreatedAt = now,
                UpdatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);

            return new ExtractDocumentResponseDto(
                extraction.Id,
                extraction.SuggestedType,
                extraction.SuggestedDocumentNumber,
                extraction.SuggestedIssueDate,
                extraction.SuggestedExpiryDate,
                extraction.SuggestedOwnerType,
                DetailsJson.FromStorage(extraction.SuggestedDetails),
                ExtractionFieldResults.FromStorage(extraction.FieldResults),
                ExtractionFieldResults.Failure(extraction.FailureCode, extraction.FailureMessage));
        }
        catch
        {
            // Best-effort cleanup so a failed request does not leave an
            // orphaned file behind a missing extraction record.
            await _storage.DeleteAsync(storedReference, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Values for the type's schema keys, for the Details form.</summary>
    private Dictionary<string, object?> SchemaDetails(
        DocumentType type,
        IReadOnlyDictionary<string, ExtractedField> fields)
    {
        var details = new Dictionary<string, object?>();

        foreach (var field in _schemas.Get(type))
        {
            if (fields.TryGetValue(field.Key, out var extracted) &&
                !string.IsNullOrWhiteSpace(extracted.Value))
            {
                details[field.Key] = extracted.Value;
            }
        }

        return details;
    }

    private static string? Value(IReadOnlyDictionary<string, ExtractedField> fields, string key)
        => fields.TryGetValue(key, out var field) && !string.IsNullOrWhiteSpace(field.Value)
            ? field.Value
            : null;

    private static DateTime? Date(IReadOnlyDictionary<string, ExtractedField> fields, string key)
        => DateOnly.TryParseExact(
            Value(fields, key),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            : null;
}
