using PROManagementSystem.Application.Clients.Queries.GetClientEntityDocuments;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IClientEntityDocumentReadRepository
{
    Task<GetClientEntityDocumentsResponseDto> GetByClientEntityIdAsync(
        Guid clientEntityId,
        bool includeDeleted,
        CancellationToken cancellationToken);
}