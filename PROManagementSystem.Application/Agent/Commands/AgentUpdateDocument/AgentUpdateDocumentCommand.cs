using MediatR;
using PROManagementSystem.Application.Documents.Commands.UpdateDocument;

namespace PROManagementSystem.Application.Agent.Commands.AgentUpdateDocument;

public sealed record AgentUpdateDocumentCommand(
    Guid DocumentId,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    IReadOnlySet<string> PresentProperties
) : IRequest<UpdateDocumentResponseDto>;
