using System.Text.Json;
using System.Text.Json.Serialization;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Common;

/// <summary>A suggested field as returned to the review form.</summary>
public sealed record ExtractedFieldDto(
    string? Value,
    ExtractedFieldSource Source,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record ExtractionFailureDto(string Code, string Message);

/// <summary>Stores and restores per-field results (DocumentExtraction.FieldResults).</summary>
public static class ExtractionFieldResults
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToStorage(IReadOnlyDictionary<string, ExtractedField> fields)
        => JsonSerializer.Serialize(
            fields.ToDictionary(
                pair => pair.Key,
                pair => new ExtractedFieldDto(
                    pair.Value.Value,
                    pair.Value.Source,
                    pair.Value.Errors,
                    pair.Value.Warnings)),
            Options);

    public static IReadOnlyDictionary<string, ExtractedFieldDto> FromStorage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, ExtractedFieldDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ExtractedFieldDto>>(json, Options)
                   ?? new Dictionary<string, ExtractedFieldDto>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, ExtractedFieldDto>();
        }
    }

    public static ExtractionFailureDto? Failure(string? code, string? message)
        => string.IsNullOrWhiteSpace(code)
            ? null
            : new ExtractionFailureDto(code, message ?? string.Empty);
}
