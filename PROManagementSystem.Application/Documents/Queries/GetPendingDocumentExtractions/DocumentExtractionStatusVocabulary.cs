namespace PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

/// <summary>
/// The extraction status vocabulary. DocumentExtraction.Status is a raw string
/// column (not an enum), so these three literals are the complete set of
/// observable states: created as "Pending", then terminal "Confirmed" or
/// "Expired". Centralised here so the listing query, its validator and the
/// handlers cannot drift apart or invent a new spelling.
/// </summary>
public static class DocumentExtractionStatusVocabulary
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Expired = "Expired";

    public static IReadOnlyList<string> All { get; } =
        new[] { Pending, Confirmed, Expired };

    /// <summary>
    /// Resolves the requested status filter, defaulting to the Pending review
    /// queue when the caller does not ask for one.
    /// </summary>
    public static string Resolve(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Pending;
        }

        return All.FirstOrDefault(
            known => string.Equals(
                known,
                status.Trim(),
                StringComparison.OrdinalIgnoreCase))
            ?? Pending;
    }
}
