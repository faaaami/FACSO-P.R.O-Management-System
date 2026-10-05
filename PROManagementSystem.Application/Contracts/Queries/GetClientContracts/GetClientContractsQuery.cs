using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientContacts;

namespace PROManagementSystem.Application.Contracts.Queries.GetClientContracts;

public sealed record GetClientContractsQuery(
    Guid ClientCompanyId,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetClientContractsResponseDto>;