using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Commands.ConfirmDocument;

public sealed record ConfirmDocumentResponseDto(
    Guid DocumentId,
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
    bool IsActive,
    DateTime CreatedAt,
    object Details
);