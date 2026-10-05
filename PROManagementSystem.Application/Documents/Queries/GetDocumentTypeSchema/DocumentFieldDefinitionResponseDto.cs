using PROManagementSystem.Application.Common.DocumentSchemas;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentTypeSchema;

/// <summary>
/// API representation of one schema field. Values are independent copies of
/// the registry metadata — callers can never mutate the shared registry.
/// </summary>
public sealed record DocumentFieldDefinitionResponseDto(
    string Key,
    string Label,
    DocumentFieldValueKind ValueKind,
    bool Required,
    int? MaxLength);