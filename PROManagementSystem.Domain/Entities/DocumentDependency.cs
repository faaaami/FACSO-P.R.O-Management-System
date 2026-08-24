using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class DocumentDependency : BaseEntity
{
    public Guid DocumentId { get; set; }

    public Guid DependsOnDocumentId { get; set; }

    public Document Document { get; set; } = null!;

    public Document DependsOnDocument { get; set; } = null!;
}