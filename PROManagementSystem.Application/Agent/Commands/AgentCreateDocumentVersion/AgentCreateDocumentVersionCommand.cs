using MediatR;
using PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;

namespace PROManagementSystem.Application.Agent.Commands.AgentCreateDocumentVersion;

public sealed record AgentCreateDocumentVersionCommand(
    Guid DocumentId,
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize
) : IRequest<CreateDocumentVersionResponseDto>;
