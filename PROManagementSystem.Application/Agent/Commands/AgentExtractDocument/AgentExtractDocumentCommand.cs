using MediatR;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;

namespace PROManagementSystem.Application.Agent.Commands.AgentExtractDocument;

public sealed record AgentExtractDocumentCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize
) : IRequest<ExtractDocumentResponseDto>;
