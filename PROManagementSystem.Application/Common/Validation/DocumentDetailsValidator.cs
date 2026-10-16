using System.Collections;
using System.Globalization;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Validation;

/// <summary>
/// Task 7B.4/7B.5: applies the type-specific <see cref="DocumentFieldDefinition"/>
/// schema to a Details payload (as bound by System.Text.Json) without ever
/// mutating or normalizing it.
///
/// Value-kind and MaxLength checks apply to every non-null JSON value that
/// matches a known schema key. Required-field presence is enforced only when
/// <paramref name="validateRequired"/> is true — today only on document
/// confirmation. Unknown keys are always allowed and preserved; the schema is
/// metadata for the fields it knows about, not a whitelist.
///
/// For types with a non-empty schema, null/omitted/empty Details is treated as
/// the canonical empty object: valid on normal writes (nothing is required)
/// but rejected on confirmation when the type declares required fields.
///
/// <see cref="DocumentType.Other"/> and unhandled enum values resolve to an
/// empty schema and are never constrained here.
/// </summary>
public sealed class DocumentDetailsValidator
{
    private readonly IDocumentTypeSchemaProvider _schemas;

    public DocumentDetailsValidator(
        IDocumentTypeSchemaProvider schemas)
    {
        _schemas = schemas;
    }

    public IReadOnlyList<ValidationFailure> Validate(
        DocumentType documentType,
        object? details,
        bool validateRequired = false)
    {
        var schema = _schemas.Get(documentType);

        if (schema.Count == 0)
        {
            return Array.Empty<ValidationFailure>();
        }

        if (!TryGetObjectElement(details, out var element))
        {
            return Array.Empty<ValidationFailure>();
        }

        var failures = new List<ValidationFailure>();

        foreach (var field in schema)
        {
            var present = element.TryGetProperty(
                field.Key,
                out var value);

            if (validateRequired &&
                field.Required &&
                (!present || value.ValueKind == JsonValueKind.Null))
            {
                failures.Add(
                    new ValidationFailure(
                        $"Details.{field.Key}",
                        $"{field.Label} is required for " +
                        $"{documentType} documents."));
                continue;
            }

            if (!present || value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            switch (field.ValueKind)
            {
                case DocumentFieldValueKind.String:
                    if (value.ValueKind != JsonValueKind.String)
                    {
                        failures.Add(
                            TypeFailure(field, "text"));
                    }
                    else if (field.MaxLength.HasValue &&
                             value.GetString()!.Length > field.MaxLength.Value)
                    {
                        failures.Add(
                            new ValidationFailure(
                                $"Details.{field.Key}",
                                $"{field.Label} must be at most " +
                                $"{field.MaxLength.Value} characters."));
                    }
                    else if (field.Format == DocumentFieldFormat.EmiratesIdNumber &&
                             !EmiratesIdNumber.IsValidFormat(value.GetString()))
                    {
                        failures.Add(
                            new ValidationFailure(
                                $"Details.{field.Key}",
                                EmiratesIdNumber.FormatMessage));
                    }

                    break;

                case DocumentFieldValueKind.Date:
                    if (value.ValueKind != JsonValueKind.String ||
                        !IsIsoDate(value.GetString()))
                    {
                        failures.Add(
                            new ValidationFailure(
                                $"Details.{field.Key}",
                                $"{field.Label} must be a date " +
                                "in YYYY-MM-DD format."));
                    }

                    break;

                case DocumentFieldValueKind.Number:
                    if (value.ValueKind != JsonValueKind.Number)
                    {
                        failures.Add(
                            TypeFailure(field, "a number"));
                    }

                    break;

                case DocumentFieldValueKind.Boolean:
                    if (value.ValueKind is not (
                        JsonValueKind.True or JsonValueKind.False))
                    {
                        failures.Add(
                            TypeFailure(field, "true or false"));
                    }

                    break;
            }
        }

        return failures;
    }

    /// <summary>
    /// Treats null/JSON-null as absent details (the canonical empty object) so
    /// the required-field check can still report missing keys on confirmation.
    /// Arrays and scalars are not object payloads: they are rejected upstream
    /// by <see cref="DetailsJson.IsObjectOrNull"/> and yield no type-specific
    /// failures here.
    /// </summary>
    private static bool TryGetObjectElement(
        object? details,
        out JsonElement element)
    {
        if (details is null)
        {
            element = EmptyObjectElement();
            return true;
        }

        if (details is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                element = jsonElement.Clone();
                return true;
            }

            if (jsonElement.ValueKind == JsonValueKind.Null)
            {
                element = EmptyObjectElement();
                return true;
            }

            element = default;
            return false;
        }

        if (details is IDictionary dictionary)
        {
            element = JsonSerializer.SerializeToElement(
                dictionary,
                dictionary.GetType());

            return element.ValueKind == JsonValueKind.Object;
        }

        element = default;
        return false;
    }

    private static bool IsIsoDate(string? value)
        => DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    private static ValidationFailure TypeFailure(
        DocumentFieldDefinition field,
        string expectation)
        => new ValidationFailure(
            $"Details.{field.Key}",
            $"{field.Label} must be {expectation}.");

    private static JsonElement EmptyObjectElement()
        => JsonDocument.Parse("{}").RootElement.Clone();
}