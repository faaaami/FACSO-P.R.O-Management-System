using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Employees.Commands.CreateEmployeeDocument;

public sealed record CreateEmployeeDocumentRequestDto(
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    object? Details
);