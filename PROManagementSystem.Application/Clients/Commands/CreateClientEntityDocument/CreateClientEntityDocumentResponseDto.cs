using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;

public sealed record CreateClientEntityDocumentResponseDto(
    Guid Id,
    Guid ClientEntityId,
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