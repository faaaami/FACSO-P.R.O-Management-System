using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// What <see cref="OpenRouterResponseParser"/> recovered from a model
/// response, before it is mapped to per-field results (source Ai). The
/// confidence is the model's own claim and is never shown to users.
/// </summary>
internal sealed record AiParsedExtraction(
    DocumentType? Type,
    string? DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? OwnerType,
    decimal Confidence)
{
    public IReadOnlyDictionary<string, object?>? Details { get; init; }
}
