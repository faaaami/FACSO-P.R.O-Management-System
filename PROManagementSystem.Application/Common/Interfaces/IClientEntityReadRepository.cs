using PROManagementSystem.Application.Clients.Queries.GetClientEntities;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityById;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IClientEntityReadRepository
{
    Task<GetClientEntitiesResponseDto> GetPagedAsync(
        Guid clientCompanyId,
        int page,
        int pageSize,
        string? search,
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<GetClientEntityByIdResponseDto?> GetByIdAsync(
    Guid clientCompanyId,
    Guid entityId,
    bool includeDeleted,
    CancellationToken cancellationToken);
}