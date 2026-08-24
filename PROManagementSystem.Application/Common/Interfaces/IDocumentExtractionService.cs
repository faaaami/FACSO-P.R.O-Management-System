using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IDocumentExtractionService
{
    Task<DocumentExtractionResult> ExtractAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken);
}

public sealed record DocumentExtractionResult(
    DocumentType? Type,
    string? DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? OwnerType,
    decimal Confidence
);