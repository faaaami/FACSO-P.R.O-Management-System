using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentScan;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentScan;

public sealed record GetAgentDocumentScanQuery(
    Guid DocumentId
) : IRequest<GetDocumentScanResponseDto>;
