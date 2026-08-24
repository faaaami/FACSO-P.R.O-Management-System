namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentDependency;

public sealed record CreateDocumentDependencyRequest(
    Guid DependsOnDocumentId
);