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

    public decimal Confidence { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime? ExpiresAt { get; set; }

    // The agent who created this extraction draft. Nullable so extraction
    // rows created before this column existed remain valid (legacy rows).
    public Guid? CreatedBy { get; set; }

    public User? CreatedByUser { get; set; }
}