namespace PROManagementSystem.Application.Agent.Commands.AgentUpdateDocument;

public sealed record AgentUpdateDocumentRequestDto(
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    object? Details
);
