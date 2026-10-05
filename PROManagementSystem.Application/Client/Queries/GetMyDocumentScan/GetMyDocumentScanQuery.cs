using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentScan;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocumentScan;

public sealed record GetMyDocumentScanQuery(
    Guid DocumentId
) : IRequest<GetDocumentScanResponseDto>;