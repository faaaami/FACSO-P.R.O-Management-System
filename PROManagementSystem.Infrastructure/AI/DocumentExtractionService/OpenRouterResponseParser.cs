using System.Globalization;
using System.Text.Json;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7C.3.2: the structured parser for OpenRouter model responses. It turns
/// one raw <see cref="OpenRouterRawResponse"/> into the existing
/// <see cref="DocumentExtractionResult"/> suggestion draft.
///
/// The OpenRouter chat/completions envelope is NOT the extraction object: the
/// model's logical JSON is extracted from
/// <c>choices[0].message.content</c> first, and only that content is parsed.
/// A missing/empty/non-string content, a malformed envelope, or a malformed
/// logical document all degrade to the neutral result
/// <c>(null, null, null, null, null, 0m)</c> — malformed AI output can never
/// create an exception at the API boundary.
///
/// Everything here is suggestion-only and non-authoritative. Nothing is
/// fabricated: unknown document types, unparseable dates, invalid detail
/// values, unknown detail keys, and over-length strings are discarded, never
/// corrected or invented. The type-specific details are restricted to the
/// <see cref="IDocumentTypeSchemaProvider"/> schema for the resolved type and
/// are then re-checked through the existing
/// <see cref="DocumentDetailsValidator"/> (required-check off) — the same
/// validation the Local provider uses, not a competing framework.
///
/// Security/privacy: the parser has no logger and no I/O. It never logs (and
/// never copies into the result) the raw model response, the document text, or
/// any credential. The result carries only the structured suggestions.
///
/// Confidence contract: the established <see cref="DocumentExtractionResult"/>
/// field is a single value. Per the 7C.3.2 combination rule, a well-formed
/// in-range model confidence is preferred; otherwise the classifier's
/// confidence is retained. Either way the returned value is validated into
/// [0, 1]. A neutral result is always 0.
/// </summary>
internal sealed class OpenRouterResponseParser : IOpenRouterResponseParser
{
    private const int MaxDocumentNumberLength = 200;
    private const int MaxOwnerTypeLength = 50;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IDocumentTypeSchemaProvider _schemas;
    private readonly DocumentDetailsValidator _validator;

    public OpenRouterResponseParser(
        IDocumentTypeSchemaProvider schemas,
        DocumentDetailsValidator validator)
    {
        _schemas = schemas;
        _validator = validator;
    }

    public DocumentExtractionResult Parse(
        OpenRouterRawResponse raw,
        DocumentType? detectedType,
        decimal classificationConfidence)
    {
        // The model's documentType is authoritative for this provider; the
        // classifier's type is context only (OpenRouterPromptBuilder rule 6).
        _ = detectedType;

        if (string.IsNullOrWhiteSpace(raw.JsonContent))
        {
            return Neutral();
        }

        try
        {
            using var envelope = JsonDocument.Parse(raw.JsonContent);
            var content = ExtractMessageContent(envelope.RootElement);

            if (string.IsNullOrWhiteSpace(content))
            {
                return Neutral();
            }

            using var logical = JsonDocument.Parse(content);

            return Map(logical.RootElement, classificationConfidence);
        }
        catch (JsonException)
        {
            return Neutral();
        }
    }

    /// <summary>
    /// Navigates the OpenRouter chat/completions envelope to the model content.
    /// Any missing/empty/mistyped step yields null (a neutral result).
    /// </summary>
    private static string? ExtractMessageContent(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(root, "choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var first = choices[0];

        if (first.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(first, "message", out var message) ||
            message.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(message, "content", out var content) ||
            content.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return content.GetString();
    }

    /// <summary>
    /// Maps the logical extraction object. Properties are matched
    /// case-insensitively to tolerate model casing drift; the exact contract
    /// names are always tried first.
    /// </summary>
    private DocumentExtractionResult Map(
        JsonElement root,
        decimal classificationConfidence)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Neutral();
        }

        var type = ResolveType(root);

        if (type is null)
        {
            return Neutral();
        }

        var documentNumber = ReadBoundedString(
            root,
            "documentNumber",
            MaxDocumentNumberLength);

        var ownerType = ReadBoundedString(
            root,
            "ownerType",
            MaxOwnerTypeLength);

        var issueDate = ReadDate(root, "issueDate");
        var expiryDate = ReadDate(root, "expiryDate");

        // An inconsistent pair is never corrected; the untrustworthy temporal
        // pair is dropped entirely.
        if (issueDate.HasValue &&
            expiryDate.HasValue &&
            issueDate.Value > expiryDate.Value)
        {
            issueDate = null;
            expiryDate = null;
        }

        var details = ReadDetails(root, type.Value);
        var confidence = ReadConfidence(root, classificationConfidence);

        return new DocumentExtractionResult(
            type,
            documentNumber,
            issueDate,
            expiryDate,
            ownerType,
            confidence)
        {
            Details = details
        };
    }

    private static DocumentType? ResolveType(JsonElement root)
    {
        if (!TryGetProperty(root, "documentType", out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();

        if (string.IsNullOrWhiteSpace(text) ||
            !Enum.TryParse<DocumentType>(
                text.Trim(),
                ignoreCase: true,
                out var parsed))
        {
            return null;
        }

        return Enum.IsDefined(parsed) ? parsed : null;
    }

    private static string? ReadBoundedString(
        JsonElement root,
        string name,
        int maxLength)
    {
        if (!TryGetProperty(root, name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();

        // Empty and over-length candidates are dropped, never truncated.
        return string.IsNullOrEmpty(text) || text.Length > maxLength
            ? null
            : text;
    }

    private static DateTime? ReadDate(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();

        if (string.IsNullOrEmpty(text) ||
            !DateOnly.TryParseExact(
                text,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return null;
        }

        // The extracted dates are persisted to "timestamp with time zone"
        // columns, so they must be explicitly UTC: an Unspecified kind is
        // rejected by Npgsql at SaveChanges time.
        return DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);
    }

    /// <summary>
    /// Preferences a well-formed in-range model confidence; otherwise retains
    /// the classifier's confidence. The returned value is always in [0, 1].
    /// </summary>
    private static decimal ReadConfidence(
        JsonElement root,
        decimal classificationConfidence)
    {
        if (TryGetProperty(root, "confidence", out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var model) &&
            model is >= 0m and <= 1m)
        {
            return model;
        }

        return classificationConfidence is >= 0m and <= 1m
            ? classificationConfidence
            : 0m;
    }

    /// <summary>
    /// Schema-restricted details extraction. Only keys present in the resolved
    /// type's schema survive; each candidate is converted to the project's
    /// established details representation (String as trimmed text, Date as a
    /// strict yyyy-MM-dd string, Number as decimal, Boolean as bool), then the
    /// assembled object is re-validated through
    /// <see cref="DocumentDetailsValidator"/> with required-checks off.
    /// </summary>
    private IReadOnlyDictionary<string, object?>? ReadDetails(
        JsonElement root,
        DocumentType type)
    {
        var schema = _schemas.Get(type);

        if (schema.Count == 0 ||
            !TryGetProperty(root, "details", out var details) ||
            details.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var extracted = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in schema)
        {
            if (!TryGetProperty(details, field.Key, out var value))
            {
                continue;
            }

            var converted = ConvertValue(field, value);

            if (converted is not null)
            {
                extracted[field.Key] = converted;
            }
        }

        if (extracted.Count == 0)
        {
            return null;
        }

        foreach (var failure in _validator.Validate(
            type,
            extracted,
            validateRequired: false))
        {
            var key = FailureKey(failure.PropertyName);

            if (key is not null)
            {
                extracted.Remove(key);
            }
        }

        return extracted.Count == 0 ? null : extracted;
    }

    private static object? ConvertValue(
        DocumentFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return field.ValueKind switch
        {
            DocumentFieldValueKind.String =>
                ConvertString(value, field.MaxLength),
            DocumentFieldValueKind.Date => ConvertDate(value),
            DocumentFieldValueKind.Number => ConvertNumber(value),
            DocumentFieldValueKind.Boolean => ConvertBoolean(value),
            _ => null,
        };
    }

    private static object? ConvertString(JsonElement value, int? maxLength)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // Match the schema's MaxLength exactly: an over-length candidate is
        // dropped, never truncated.
        return maxLength.HasValue && text.Length > maxLength.Value
            ? null
            : text;
    }

    private static object? ConvertDate(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();

        // Strict yyyy-MM-dd only; anything else is discarded.
        if (string.IsNullOrEmpty(text) ||
            !DateOnly.TryParseExact(
                text,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return null;
        }

        return text;
    }

    private static object? ConvertNumber(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDecimal(out var number))
        {
            return null;
        }

        return number;
    }

    private static object? ConvertBoolean(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

    /// <summary>
    /// Case-insensitive property lookup with an exact-name fast path. Avoids
    /// reflection/dynamic parsing entirely.
    /// </summary>
    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(
                    property.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
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

    private static DocumentExtractionResult Neutral()
        => new(null, null, null, null, null, 0m);
}
