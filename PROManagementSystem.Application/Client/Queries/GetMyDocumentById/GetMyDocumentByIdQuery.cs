using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocumentById;

public sealed record GetMyDocumentByIdQuery(
    Guid DocumentId
) : IRequest<GetDocumentByIdResponseDto?>;