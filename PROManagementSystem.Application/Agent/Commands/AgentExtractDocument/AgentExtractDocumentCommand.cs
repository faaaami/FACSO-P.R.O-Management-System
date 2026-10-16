using MediatR;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Agent.Commands.AgentExtractDocument;

/// <param name="DocumentType">Chosen by the agent before upload.</param>
public sealed record AgentExtractDocumentCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize,
    DocumentType DocumentType
) : IRequest<ExtractDocumentResponseDto>;
