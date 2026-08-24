using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityById;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IClientCompanyReadRepository
{
    Task<GetClientCompaniesResponseDto> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool includeDeleted,
        CancellationToken cancellationToken);




    Task<GetClientCompanyByIdResponseDto?> GetByIdAsync(
    Guid id,
    bool includeDeleted,
    CancellationToken cancellationToken);


  
}

