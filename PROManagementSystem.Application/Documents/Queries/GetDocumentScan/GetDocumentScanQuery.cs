using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentScan;

public sealed record GetDocumentScanQuery(
    Guid DocumentId
) : IRequest<GetDocumentScanResponseDto>;