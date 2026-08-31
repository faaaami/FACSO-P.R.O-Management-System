using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentById;

public sealed record GetAgentDocumentByIdQuery(
    Guid DocumentId
) : IRequest<GetDocumentByIdResponseDto?>;
