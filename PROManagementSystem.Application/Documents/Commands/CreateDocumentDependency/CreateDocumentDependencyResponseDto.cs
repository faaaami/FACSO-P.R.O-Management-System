namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentDependency;

public sealed record CreateDocumentDependencyResponseDto(
    Guid Id,
    Guid DocumentId,
    Guid DependsOnDocumentId,
    DateTime CreatedAt
);