using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Commands.UpdateDocument;

public sealed record UpdateDocumentResponseDto(
    Guid Id,
    Guid? ClientEntityId,
    Guid? EmployeeId,
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    bool IsActive,
    DateTime UpdatedAt,
    object Details
);