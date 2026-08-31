using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentVersions;

public sealed record GetAgentDocumentVersionsQuery(
    Guid DocumentId
) : IRequest<GetDocumentVersionsResponseDto>;
