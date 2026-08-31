using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentClientById;

public sealed record GetAgentClientByIdQuery(
    Guid ClientId
) : IRequest<GetClientCompanyByIdResponseDto?>;
