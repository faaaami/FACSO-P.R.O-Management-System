using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IDocumentExpiryReadRepository
{
    Task<GetExpiringDocumentsResponseDto> GetExpiringAsync(
        int days,
        int page,
        int pageSize,
        bool includeExpired,
        CancellationToken cancellationToken);
}