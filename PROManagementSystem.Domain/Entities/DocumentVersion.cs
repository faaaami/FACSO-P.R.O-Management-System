using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }

    public int VersionNumber { get; set; }

    public string FileUrl { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public Document Document { get; set; } = null!;
}