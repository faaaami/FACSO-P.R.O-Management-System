using MediatR;
using PROManagementSystem.Application.Employees.Commands.CreateEmployeeDocument;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Agent.Commands.AgentRegisterEmployeeDocument;

public sealed record AgentRegisterEmployeeDocumentCommand(
    Guid EmployeeId,
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    object? Details
) : IRequest<CreateEmployeeDocumentResponseDto>;
