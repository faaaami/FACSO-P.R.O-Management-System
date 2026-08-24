using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientCompanies;

public sealed class GetClientCompaniesQueryHandler
    : IRequestHandler<
        GetClientCompaniesQuery,
        GetClientCompaniesResponseDto>
{
    private readonly IClientCompanyReadRepository _repository;

    public GetClientCompaniesQueryHandler(
        IClientCompanyReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetClientCompaniesResponseDto> Handle(
        GetClientCompaniesQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.IncludeDeleted,
            cancellationToken);
    }
}