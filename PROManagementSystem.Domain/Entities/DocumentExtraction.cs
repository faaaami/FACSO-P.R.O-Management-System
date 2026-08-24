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

    public decimal Confidence { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime? ExpiresAt { get; set; }
}