using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentExtraction;

/// <summary>
/// A draft's review payload. The internal storage reference is deliberately
/// NOT part of this contract: the dedicated file endpoint streams the bytes, so
/// the server-side storage key is never exposed to the browser.
/// </summary>
public sealed record GetDocumentExtractionResponseDto(
    Guid ExtractionId,
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
    object SuggestedDetails
);