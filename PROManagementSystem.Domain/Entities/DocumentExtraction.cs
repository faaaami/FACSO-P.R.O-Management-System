using PROMangementSystem.Domain.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Domain.Entities;

public class DocumentExtraction : BaseEntity
{
    public string FileName { get; set; } = string.Empty;

    public string? FileUrl { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DocumentType? SuggestedType { get; set; }

    public string? SuggestedDocumentNumber { get; set; }

    public DateTime? SuggestedIssueDate { get; set; }

    public DateTime? SuggestedExpiryDate { get; set; }

    public string? SuggestedOwnerType { get; set; }

    // Non-authoritative extraction/AI suggestions for type-specific details
    // (Task 7B.2). A JSON object serialized with System.Text.Json (keys follow
    // the schema registry's DocumentFieldDefinition.Key contract). "No
    // suggestions" is the empty object "{}"; this string is never null. OCR
    // text must never be stored here.
    public string SuggestedDetails { get; set; } = "{}";

    // Per-field results for the review form: a JSON object keyed like the
    // suggestions, each { value, source, errors, warnings }. Never "null".
    public string FieldResults { get; set; } = "{}";

    // Why extraction could not read (part of) the document, for the user;
    // null when nothing went wrong.
    public string? FailureCode { get; set; }

    public string? FailureMessage { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime? ExpiresAt { get; set; }

    // The agent who created this extraction draft. Nullable so extraction
    // rows created before this column existed remain valid (legacy rows).
    public Guid? CreatedBy { get; set; }

    public User? CreatedByUser { get; set; }
}