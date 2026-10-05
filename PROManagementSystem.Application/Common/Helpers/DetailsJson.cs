using System.Collections;
using System.Text.Json;

namespace PROManagementSystem.Application.Common.Helpers;

/// <summary>
/// Converts type-specific document details between the JSON-object form used
/// at the HTTP boundary and the JSON string persisted in the jsonb column.
/// A JSON <c>null</c> and an omitted value are both treated as "no details"
/// and round-trip to the canonical empty object (Task 7B.2/7B.3).
/// </summary>
public static class DetailsJson
{
    /// <summary>
    /// Converts a JSON object (as bound by System.Text.Json) into the string
    /// persisted in the jsonb column. <c>null</c> maps to the canonical empty
    /// object. Non-object values must be rejected by the validators before
    /// this method is reached.
    /// </summary>
    public static string ToStorage(object? value)
    {
        if (value is null)
        {
            return "{}";
        }

        return value is JsonElement element
            ? element.GetRawText()
            : JsonSerializer.Serialize(value, value.GetType());
    }

    /// <summary>
    /// Converts the persisted jsonb string back into a JSON object for the
    /// API response. Missing or empty values fall back to the empty object.
    /// </summary>
    public static object FromStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "{}";
        }

        using var document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

    /// <summary>
    /// Validator predicate: only a JSON object (or <c>null</c>, meaning "no
    /// details") is accepted. Arrays, strings, numbers and booleans are
    /// rejected so the stored value is always a JSON object.
    /// </summary>
    public static bool IsObjectOrNull(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind is JsonValueKind.Object or JsonValueKind.Null;
        }

        return value is IDictionary;
    }
}