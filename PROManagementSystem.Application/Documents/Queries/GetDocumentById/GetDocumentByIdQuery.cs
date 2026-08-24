using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentById;

public sealed record GetDocumentByIdQuery(
    Guid DocumentId,
    bool IncludeDeleted = false
) : IRequest<GetDocumentByIdResponseDto?>;