using PROManagementSystem.Application.Documents.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

/// <summary>
/// The extraction draft. <see cref="Fields"/> carries each suggested value
/// with its source and validation errors/warnings; <see cref="Failure"/>
/// explains, in words for staff, why (part of) the document could not be
/// read. The Suggested* properties repeat the field values for convenience.
/// </summary>
public sealed record ExtractDocumentResponseDto(
    Guid ExtractionId,
    DocumentType? SuggestedType,
    string? SuggestedDocumentNumber,
    DateTime? SuggestedIssueDate,
    DateTime? SuggestedExpiryDate,
    string? SuggestedOwnerType,
    object SuggestedDetails,
    IReadOnlyDictionary<string, ExtractedFieldDto> Fields,
    ExtractionFailureDto? Failure
);
