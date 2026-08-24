using MediatR;

namespace PROManagementSystem.Application.Documents.Commands.DeleteDocument;

public sealed record DeleteDocumentCommand(
    Guid DocumentId
) : IRequest<DeleteDocumentResponseDto>;