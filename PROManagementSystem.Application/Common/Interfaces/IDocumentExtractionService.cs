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
)
{
    /// <summary>
    /// Task 7B.5: type-specific field suggestions destined for
    /// <c>DocumentExtraction.SuggestedDetails</c>. Null (the default) means "no
    /// suggestions" and persists as "{}". Never populated by the Mock provider;
    /// populated by the Local provider through
    /// <see cref="IDocumentDetailsExtractor"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Details { get; init; }
}