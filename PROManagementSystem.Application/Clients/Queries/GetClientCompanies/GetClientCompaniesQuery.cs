using MediatR;

namespace PROManagementSystem.Application.Clients.Queries.GetClientCompanies;

public sealed record GetClientCompaniesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeDeleted = false
) : IRequest<GetClientCompaniesResponseDto>;