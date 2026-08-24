using MediatR;

namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentDependency;

public sealed record CreateDocumentDependencyCommand(
    Guid DocumentId,
    Guid DependsOnDocumentId
) : IRequest<CreateDocumentDependencyResponseDto>;