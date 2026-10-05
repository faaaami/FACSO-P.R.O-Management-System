using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentTypeSchema;

public sealed record DocumentTypeSchemaResponseDto(
    DocumentType DocumentType,
    IReadOnlyList<DocumentFieldDefinitionResponseDto> Fields);