using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// Optional AI step (Extraction:Ai:Enabled). Reads field values from
/// recognised document text for the type staff selected.
/// </summary>
public interface IAiFieldExtractor
{
    Task<AiFieldExtraction> ExtractAsync(
        string documentText,
        DocumentType documentType,
        CancellationToken cancellationToken);
}

/// <param name="Available">False when the AI service could not be reached or answered unusably.</param>
/// <param name="Values">
/// "documentNumber", "issueDate", "expiryDate" and schema keys; dates are
/// yyyy-MM-dd. Contains only values the model returned and that passed
/// schema validation.
/// </param>
public sealed record AiFieldExtraction(
    bool Available,
    IReadOnlyDictionary<string, string> Values)
{
    public static readonly AiFieldExtraction Unavailable =
        new(false, new Dictionary<string, string>());
}
