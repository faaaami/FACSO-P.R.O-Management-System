using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentTypeSchema;

/// <summary>
/// Reads the read-only schema registry for one document type. This is
/// metadata-only: no database, storage, OCR, classification, or extraction
/// access. The value is the raw route value and is parsed with the project's
/// case-insensitive enum convention inside the handler.
/// </summary>
public sealed record GetDocumentTypeSchemaQuery(
    string DocumentType
) : IRequest<DocumentTypeSchemaResponseDto>;