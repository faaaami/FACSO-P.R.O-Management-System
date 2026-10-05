using MediatR;
using PROManagementSystem.Application.Documents.Commands.ConfirmDocument;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Agent.Commands.AgentConfirmExtraction;

public sealed record AgentConfirmExtractionCommand(
    Guid ExtractionId,
    Guid? EmployeeId,
    Guid? ClientEntityId,
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    object? Details
) : IRequest<ConfirmDocumentResponseDto>;
