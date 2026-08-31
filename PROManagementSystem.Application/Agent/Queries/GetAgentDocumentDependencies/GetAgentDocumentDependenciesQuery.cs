using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentDependencies;

public sealed record GetAgentDocumentDependenciesQuery(
    Guid DocumentId
) : IRequest<GetDocumentDependenciesResponseDto>;
