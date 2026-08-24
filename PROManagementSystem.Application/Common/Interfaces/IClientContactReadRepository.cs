using PROManagementSystem.Application.Clients.Queries.GetClientContacts;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IClientContactReadRepository
{
    Task<GetClientContactsResponseDto> GetPagedAsync(
        Guid clientCompanyId,
        int page,
        int pageSize,
        string? search,
        bool includeDeleted,
        CancellationToken cancellationToken);
}