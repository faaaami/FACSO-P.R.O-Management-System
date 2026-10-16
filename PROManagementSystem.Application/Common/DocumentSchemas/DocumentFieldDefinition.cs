namespace PROManagementSystem.Application.Common.DocumentSchemas;

/// <summary>
/// Immutable description of one type-specific document field. Instances are
/// static metadata shared through <see cref="IDocumentTypeSchemaProvider"/>;
/// they carry nothing mutable and no persistence/validation behavior.
///
/// <see cref="Key"/> is a stable camelCase contract used by future API review
/// metadata, validation, and AI extraction. A key must not be duplicated
/// within one schema and must keep the same meaning across document types.
///
/// <see cref="Required"/> marks a field mandatory for a valid confirmed
/// document of its type. It is intentionally conservative: only each type's
/// primary identifier is currently required. Common validation (owner, dates,
/// DocumentNumber) stays where it is today and is not expressed here.
///
/// <see cref="MaxLength"/> is metadata for future application validation — not
/// a database constraint. It applies only to <see cref="String"/> fields where
/// a sensible bounded length is obvious and is null for other
/// <see cref="DocumentFieldValueKind"/> values.
/// </summary>
public sealed record DocumentFieldDefinition(
    string Key,
    string Label,
    DocumentFieldValueKind ValueKind,
    bool Required = false,
    int? MaxLength = null,
    DocumentFieldFormat Format = DocumentFieldFormat.None);

/// <summary>
/// An additional format rule for a String field, enforced by
/// <see cref="Validation.DocumentDetailsValidator"/> wherever details are saved.
/// </summary>
public enum DocumentFieldFormat
{
    None = 0,

    /// <summary>784-YYYY-NNNNNNN-N (see <see cref="Validation.EmiratesIdNumber"/>).</summary>
    EmiratesIdNumber = 1
}