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

    public bool IsActive { get; set; } = true;

    public ClientEntity? ClientEntity { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; }
        = new List<DocumentVersion>();
}