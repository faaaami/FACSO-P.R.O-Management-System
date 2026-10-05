using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.DocumentSchemas;

/// <summary>
/// Answers "what type-specific fields belong to a document type?". The
/// returned collection is read-only: callers must never be able to mutate the
/// global registry.
///
/// This interface is metadata-only by design. It does not validate values and
/// carries no persistence or AI dependency.
/// </summary>
public interface IDocumentTypeSchemaProvider
{
    /// <summary>
    /// Returns the read-only field definitions for a document type.
    /// <see cref="DocumentType.Other"/> and unhandled/unknown enum values
    /// resolve to an empty collection — never null and never an exception.
    /// </summary>
    IReadOnlyList<DocumentFieldDefinition> Get(
        DocumentType documentType);
}