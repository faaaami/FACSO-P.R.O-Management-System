using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentClients;

public sealed record GetAgentClientsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<GetClientCompaniesResponseDto>;
