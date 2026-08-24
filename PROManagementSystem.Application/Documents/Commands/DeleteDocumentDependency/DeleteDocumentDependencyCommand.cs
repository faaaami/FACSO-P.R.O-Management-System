using MediatR;

namespace PROManagementSystem.Application.Documents.Commands.DeleteDocumentDependency;

public sealed record DeleteDocumentDependencyCommand(
    Guid DocumentId,
    Guid DependencyId
) : IRequest<DeleteDocumentDependencyResponseDto>;