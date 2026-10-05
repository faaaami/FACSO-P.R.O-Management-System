using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

/// <summary>
/// Paged list of document-extraction drafts awaiting human review.
/// </summary>
public sealed record GetPendingDocumentExtractionsResponseDto(
    List<PendingDocumentExtractionListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

/// <summary>
/// One extraction draft. Carries only the non-authoritative suggestions plus
/// the review state. The internal storage reference (FileUrl) is deliberately
/// absent: the dedicated file endpoint streams the bytes, so the storage key
/// is never handed to the browser.
/// </summary>
public sealed record PendingDocumentExtractionListItemDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    DocumentType? SuggestedType,
    string? SuggestedDocumentNumber,
    DateTime? SuggestedIssueDate,
    DateTime? SuggestedExpiryDate,
    string? SuggestedOwnerType,
    decimal Confidence,
    string Status,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    Guid? CreatedBy
);
