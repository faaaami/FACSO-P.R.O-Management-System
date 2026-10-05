using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.AI.DocumentClassification;

/// <summary>
/// Deterministic, rule-based document classifier (Task 6C). Text recovered by
/// the extraction pipeline is normalised (lower-cased; letters and digits only;
/// punctuation and whitespace collapsed to single spaces) and scored against a
/// fixed, conservative indicator vocabulary for every supported document type.
///
/// Confidence is the fraction of a type's indicator weight that matched
/// (matched / maxPossibleWeight). Every type currently carries two strong
/// indicators (weight 3) and two supporting indicators (weight 1), giving a
/// uniform maximum of 8. A document is only labelled when its top-scoring type
/// is unambiguous (no matched-weight tie with the runner-up) and its confidence
/// is at least <see cref="MinimumConfidenceThreshold"/>; otherwise the result
/// is null/0. The classifier never fabricates an "Other" label.
///
/// These are initial conservative rules. They validate classification
/// mechanics against synthetic fixtures; they do not claim production-grade
/// real-world accuracy. The vocabulary is intentionally small and stable, and
/// must be validated against representative anonymised samples before being
/// relied on for real documents.
/// </summary>
public sealed class RuleBasedDocumentClassifier : IDocumentClassifier
{
    /// <summary>
    /// Minimum confidence at (or above) which a document type is suggested.
    /// 0.50m means "one strong indicator plus one supporting indicator" of
    /// the uniform eight-weight indicator set is required for a suggestion.
    /// Below this threshold the classifier returns null/0.
    /// </summary>
    public const decimal MinimumConfidenceThreshold = 0.50m;

    private const int Strong = 3;
    private const int Supporting = 1;

    private readonly record struct Indicator(string Phrase, int Weight);

    private static readonly IReadOnlyDictionary<DocumentType, IReadOnlyList<Indicator>> Rules =
        new Dictionary<DocumentType, IReadOnlyList<Indicator>>
        {
            [DocumentType.Passport] = new[]
            {
                new Indicator("passport", Strong),
                new Indicator("passport number", Strong),
                new Indicator("nationality", Supporting),
                new Indicator("date of birth", Supporting),
            },

            [DocumentType.Visa] = new[]
            {
                new Indicator("visa", Strong),
                new Indicator("residence visa", Strong),
                new Indicator("visa number", Supporting),
                new Indicator("entry permit", Supporting),
            },

            [DocumentType.EmiratesId] = new[]
            {
                new Indicator("emirates id", Strong),
                new Indicator("emirates identity card", Strong),
                new Indicator("id number", Supporting),
                new Indicator("identity card number", Supporting),
            },

            [DocumentType.LaborCard] = new[]
            {
                new Indicator("labor card", Strong),
                new Indicator("labour card", Strong),
                new Indicator("labor card number", Supporting),
                new Indicator("labour card number", Supporting),
            },

            [DocumentType.TradeLicense] = new[]
            {
                new Indicator("trade license", Strong),
                new Indicator("trade licence", Strong),
                new Indicator("license number", Supporting),
                new Indicator("licence number", Supporting),
            },

            [DocumentType.EstablishmentCard] = new[]
            {
                new Indicator("establishment card", Strong),
                new Indicator("establishment number", Strong),
                new Indicator("economic department", Supporting),
                new Indicator("company name", Supporting),
            },
        };

    public Task<DocumentClassificationResult> ClassifyAsync(
        string? text,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = Normalize(text);

        if (normalized.Length == 0)
        {
            return Task.FromResult(
                new DocumentClassificationResult(null, 0m));
        }

        var candidates = new List<(DocumentType Type, int Score, decimal Confidence)>();

        foreach (var pair in Rules)
        {
            var matched = 0;
            var maxWeight = 0;

            foreach (var indicator in pair.Value)
            {
                maxWeight += indicator.Weight;

                if (normalized.Contains(
                        indicator.Phrase,
                        StringComparison.Ordinal))
                {
                    matched += indicator.Weight;
                }
            }

            if (matched <= 0)
            {
                continue;
            }

            var confidence = maxWeight > 0
                ? decimal.Round(
                    (decimal)matched / maxWeight,
                    4,
                    MidpointRounding.AwayFromZero)
                : 0m;

            candidates.Add((pair.Key, matched, confidence));
        }

        if (candidates.Count == 0)
        {
            return Task.FromResult(
                new DocumentClassificationResult(null, 0m));
        }

        candidates.Sort((a, b) =>
        {
            var cmp = b.Score.CompareTo(a.Score);

            return cmp != 0
                ? cmp
                : a.Type.CompareTo(b.Type);
        });

        var top = candidates[0];

        var isTie = candidates.Count > 1
            && candidates[1].Score == top.Score;

        if (isTie)
        {
            return Task.FromResult(
                new DocumentClassificationResult(null, 0m));
        }

        if (top.Confidence < MinimumConfidenceThreshold)
        {
            return Task.FromResult(
                new DocumentClassificationResult(null, 0m));
        }

        return Task.FromResult(
            new DocumentClassificationResult(top.Type, top.Confidence));
    }

    /// <summary>
    /// Conservative normalisation: lower-case, keep only letters and digits,
    /// replace all other characters with a space, then collapse whitespace to
    /// single spaces. This is deterministic and explainable — it does not
    /// implement fuzzy matching or phonetic similarity.
    /// </summary>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var buffer = new char[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            buffer[i] = char.IsLetterOrDigit(c)
                ? char.ToLowerInvariant(c)
                : ' ';
        }

        return string.Join(
            ' ',
            new string(buffer).Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));
    }
}
