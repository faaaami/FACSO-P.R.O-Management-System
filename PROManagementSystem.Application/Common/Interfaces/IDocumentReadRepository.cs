using PROManagementSystem.Application.Documents.Queries.GetDocumentById;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IDocumentReadRepository
{
    Task<GetDocumentByIdResponseDto?> GetByIdAsync(
        Guid documentId,
        bool includeDeleted,
        CancellationToken cancellationToken);
}