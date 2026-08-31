using MediatR;
using PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Agent.Commands.AgentRegisterEntityDocument;

public sealed record AgentRegisterEntityDocumentCommand(
    Guid EntityId,
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize
) : IRequest<CreateClientEntityDocumentResponseDto>;
