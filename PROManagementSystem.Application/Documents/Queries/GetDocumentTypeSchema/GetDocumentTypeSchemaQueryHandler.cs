using MediatR;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentTypeSchema;

/// <summary>
/// Resolves a document type schema through the shared read-only registry.
/// The registry stays the single source of truth: this handler only maps its
/// output to API DTOs and performs no database/storage/OCR work.
/// </summary>
public sealed class GetDocumentTypeSchemaQueryHandler
    : IRequestHandler<
        GetDocumentTypeSchemaQuery,
        DocumentTypeSchemaResponseDto>
{
    private readonly IDocumentTypeSchemaProvider _schemaProvider;

    public GetDocumentTypeSchemaQueryHandler(
        IDocumentTypeSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public Task<DocumentTypeSchemaResponseDto> Handle(
        GetDocumentTypeSchemaQuery request,
        CancellationToken cancellationToken)
    {
        // Follows the project's case-insensitive enum parsing convention
        // (e.g. UpdateRenewalTaskStatus). Unknown values must never fall
        // through to the empty "Other" schema, so they reject with a
        // standard 400 BAD_REQUEST via the global exception middleware.
        if (!Enum.TryParse<DocumentType>(
                request.DocumentType,
                ignoreCase: true,
                out var documentType) ||
            !Enum.IsDefined(documentType))
        {
            throw new ArgumentException(
                $"Invalid document type value: '{request.DocumentType}'.");
        }

        var fields = _schemaProvider.Get(documentType);

        var response = new DocumentTypeSchemaResponseDto(
            documentType,
            fields
                .Select(f => new DocumentFieldDefinitionResponseDto(
                    f.Key,
                    f.Label,
                    f.ValueKind,
                    f.Required,
                    f.MaxLength))
                .ToList());

        return Task.FromResult(response);
    }
}