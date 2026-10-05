using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Commands.UpdateDocument;

public sealed record UpdateDocumentRequestDto(
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    bool IsActive,
    object? Details
);