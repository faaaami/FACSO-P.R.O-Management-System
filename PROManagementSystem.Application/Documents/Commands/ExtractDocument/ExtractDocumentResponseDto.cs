using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

public sealed record ExtractDocumentResponseDto(
    Guid ExtractionId,
    DocumentType? SuggestedType,
    string? SuggestedDocumentNumber,
    DateTime? SuggestedIssueDate,
    DateTime? SuggestedExpiryDate,
    string? SuggestedOwnerType,
    decimal Confidence,
    object SuggestedDetails
);