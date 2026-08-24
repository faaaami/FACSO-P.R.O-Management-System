using PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IDocumentVersionReadRepository
{
    Task<GetDocumentVersionsResponseDto> GetByDocumentIdAsync(
        Guid documentId,
        bool includeDeleted,
        CancellationToken cancellationToken);
}