using System.Globalization;
using System.Text;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7B.5: deterministic, schema-driven field extractor for the Local
/// extraction provider. The recovered text is scanned for
/// "LABEL: value" lines; the suggested <see cref="DocumentType"/> selects a
/// schema from <see cref="IDocumentTypeSchemaProvider"/>; each schema key that
/// has a known synonym label is parsed into the value kind the schema
/// declares; and the assembled object passes through
/// <see cref="DocumentDetailsValidator"/> (required-check off) so only schema-
/// valid candidates survive.
///
/// This is deliberately conservative and honest:
/// - Only schema-defined keys are produced. Unknown labels are ignored and
///   never invented into keys (future AI providers may add arbitrary keys at
///   the persistence layer, but not this extractor).
/// - Partial extraction is valid: optional and even required fields may be
///   missing because suggested values are non-authoritative and the required-
///   field rule applies only at confirmation.
/// - Individual invalid candidates are dropped while valid ones are retained;
///   if nothing valid remains the result is empty (persisted as "{}").
/// - A normalised label match is case-insensitive with whitespace collapsed;
///   values preserve their casing and Unicode and are only trimmed, collapsed
///   and stripped of trailing punctuation.
/// - No extraction confidence is produced and nothing here ever touches
///   classification confidence.
///
/// Failure handling mirrors the pipeline: malformed or missing values degrade
/// to empty/partial results; caller cancellation and any unexpected exception
/// from the schema/validator propagate untouched. No blanket exception
/// swallowing.
///
/// Accuracy is only claimed for the synthetic, anonymised "LABEL: value"
/// fixtures the extraction tests use. Real-world document formats are not
/// modelled, so no production accuracy is claimed.
/// </summary>
public sealed class RuleBasedDocumentDetailsExtractor
    : IDocumentDetailsExtractor
{
    /// <summary>
    /// Schema-key → synonym labels for extraction. A field is only extracted
    /// when its schema key appears here with a label that exists in the
    /// recovered text. Labels are matched case-insensitively. Fields without a
    /// stable, unambiguous label are intentionally left unextracted rather
    /// than guessed.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> LabelSynonyms =
        new Dictionary<string, string[]>
        {
            ["passportNumber"] = new[] { "PASSPORT NUMBER" },
            ["visaNumber"] = new[] { "VISA NUMBER" },
            ["entryPermitNumber"] = new[] { "ENTRY PERMIT" },
            ["idNumber"] = new[]
            {
                "ID NUMBER",
                "IDENTITY CARD NUMBER",
            },
            ["cardNumber"] = new[] { "CARD NUMBER" },
            ["laborCardNumber"] = new[]
            {
                "LABOR CARD NUMBER",
                "LABOUR CARD NUMBER",
            },
            ["licenseNumber"] = new[]
            {
                "LICENSE NUMBER",
                "LICENCE NUMBER",
            },
            ["establishmentNumber"] = new[] { "ESTABLISHMENT NUMBER" },
            ["nationality"] = new[] { "NATIONALITY" },
            ["companyName"] = new[] { "COMPANY NAME" },
            ["economicDepartment"] = new[] { "ECONOMIC DEPARTMENT" },
            ["dateOfBirth"] = new[] { "DATE OF BIRTH" },
            ["issueDate"] = new[] { "ISSUE DATE" },
            ["expiryDate"] = new[] { "EXPIRY DATE" },
        };

    private readonly IDocumentTypeSchemaProvider _schemas;
    private readonly DocumentDetailsValidator _validator;

    public RuleBasedDocumentDetailsExtractor(
        IDocumentTypeSchemaProvider schemas,
        DocumentDetailsValidator validator)
    {
        _schemas = schemas;
        _validator = validator;
    }

    public Task<DocumentDetailsExtractionResult> ExtractAsync(
        string? text,
        DocumentType? suggestedType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var details = Extract(text, suggestedType);

        return Task.FromResult(
            new DocumentDetailsExtractionResult(details));
    }

    private IReadOnlyDictionary<string, object?> Extract(
        string? text,
        DocumentType? suggestedType)
    {
        if (!suggestedType.HasValue ||
            string.IsNullOrWhiteSpace(text))
        {
            return EmptyDetails();
        }

        var schema = _schemas.Get(suggestedType.Value);

        if (schema.Count == 0)
        {
            return EmptyDetails();
        }

        // The schema keys with known labels are the only fields this pass can
        // extract; each line contributes at most one value.
        var lines = SplitLines(text);
        var extracted = new Dictionary<string, object?>();

        foreach (var field in schema)
        {
            if (!LabelSynonyms.TryGetValue(
                    field.Key,
                    out var labels) ||
                labels.Length == 0)
            {
                continue;
            }

            var value = ParseFieldValue(
                lines,
                field,
                labels);

            if (value is not null)
            {
                extracted[field.Key] = value;
            }
        }

        if (extracted.Count == 0)
        {
            return EmptyDetails();
        }

        // Reuse the exact 7B.4 validation logic (required-check off) on the
        // assembled object; any offending candidate is dropped while the rest
        // survives. The property name carries the key as "Details.<key>".
        foreach (var failure in _validator.Validate(
            suggestedType.Value,
            extracted,
            validateRequired: false))
        {
            var key = FailureKey(failure.PropertyName);

            if (key is not null)
            {
                extracted.Remove(key);
            }
        }

        return extracted;
    }

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

    private static object? ParseFieldValue(
        string[] lines,
        DocumentFieldDefinition field,
        string[] labels)
    {
        foreach (var line in lines)
        {
            var (label, value) = SplitLabel(line);

            if (label.Length == 0)
            {
                continue;
            }

            if (!labels.Any(synonym =>
                    string.Equals(
                        label,
                        synonym,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        NormalizeLabel(label),
                        synonym,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // First matching line wins; a single document typically carries a
            // field once, and repeated scans should not double-populate.
            return ParseValue(field, value);
        }

        return null;
    }

    /// <summary>
    /// Splits a "LABEL: value" line at the first colon. The label is trimmed
    /// and matched case-insensitively (whitespace needs to survive intact, so
    /// the raw label is compared directly before any normalisation); the value
    /// is everything after the first colon.
    /// </summary>
    private static (string Label, string Value) SplitLabel(string line)
    {
        var separator = line.IndexOf(':');

        if (separator < 0)
        {
            return (line.Trim(), string.Empty);
        }

        return (
            line[..separator].Trim(),
            line[(separator + 1)..].Trim());
    }

    /// <summary>
    /// Normalises a label for matching: uppercase, collapse internal whitespace
    /// runs, strip surrounding whitespace. Used as a fallback so labels such
    /// as "PASSPORT  NUMBER" still match "PASSPORT NUMBER".
    /// </summary>
    private static string NormalizeLabel(string label)
    {
        var collapsed = CollapseWhitespace(label);

        return collapsed.ToUpperInvariant();
    }

    private static object? ParseValue(
        DocumentFieldDefinition field,
        string value)
    {
        switch (field.ValueKind)
        {
            case DocumentFieldValueKind.String:
                return ParseString(value, field.MaxLength);

            case DocumentFieldValueKind.Date:
                return ParseDate(value);

            case DocumentFieldValueKind.Number:
                return ParseNumber(value);

            case DocumentFieldValueKind.Boolean:
                return ParseBoolean(value);
        }

        return null;
    }

    private static object? ParseString(
        string value,
        int? maxLength)
    {
        var collapsed = CollapseWhitespace(value);
        var cleaned = StripTrailingPunctuation(collapsed);

        if (cleaned.Length == 0)
        {
            return null;
        }

        // Never truncate MaxLength: an over-length candidate is dropped.
        if (maxLength.HasValue &&
            cleaned.Length > maxLength.Value)
        {
            return null;
        }

        return cleaned;
    }

    private static object? ParseDate(string value)
    {
        var collapsed = CollapseWhitespace(value);

        // Strict yyyy-MM-dd only — identical to the validator's expectation.
        if (!DateOnly.TryParseExact(
                collapsed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return null;
        }

        return collapsed;
    }

    private static object? ParseNumber(string value)
    {
        var collapsed = CollapseWhitespace(value);

        if (!decimal.TryParse(
                collapsed,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return null;
        }

        return number;
    }

    private static object? ParseBoolean(string value)
    {
        var collapsed = CollapseWhitespace(value);

        if (collapsed.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (collapsed.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    /// <summary>
    /// Collapses every interior whitespace run to a single space, trims the
    /// ends, and preserves the original casing and Unicode of the value.
    /// </summary>
    private static string CollapseWhitespace(string value)
    {
        var collapsed = new StringBuilder(value.Length);

        var inWhitespace = false;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                inWhitespace = collapsed.Length > 0;
                continue;
            }

            if (inWhitespace)
            {
                collapsed.Append(' ');
                inWhitespace = false;
            }

            collapsed.Append(c);
        }

        return collapsed.ToString();
    }

    /// <summary>
    /// Removes trailing punctuation (e.g. a stray period at the end of an OCR'd
    /// value) without touching the leading content.
    /// </summary>
    private static string StripTrailingPunctuation(string value)
    {
        var end = value.Length;

        while (end > 0 &&
               char.IsPunctuation(value[end - 1]))
        {
            end--;
        }

        return value[..end];
    }

    private static string? FailureKey(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName) ||
            !propertyName.StartsWith("Details.", StringComparison.Ordinal))
        {
            return null;
        }

        return propertyName["Details.".Length..];
    }

    private static IReadOnlyDictionary<string, object?> EmptyDetails()
        => new Dictionary<string, object?>();
}