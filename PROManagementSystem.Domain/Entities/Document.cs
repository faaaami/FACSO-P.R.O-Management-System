using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;
using System.Xml.Linq;

namespace PROManagementSystem.Domain.Entities;

public class Document : BaseEntity
{
    // Exactly one of these should be set:
    // ClientEntityId OR EmployeeId

    public Guid? ClientEntityId { get; set; }

    public Guid? EmployeeId { get; set; }

    public DocumentType Type { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime? IssueDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? FileUrl { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    // Authoritative, human-confirmed type-specific details (Task 7B.2).
    // A JSON object serialized with System.Text.Json (keys follow the schema
    // registry's DocumentFieldDefinition.Key contract). "No details" is the
    // empty object "{}"; this string is never null. OCR text must never be
    // stored here.
    public string Details { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public ClientEntity? ClientEntity { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; }
        = new List<DocumentVersion>();
}