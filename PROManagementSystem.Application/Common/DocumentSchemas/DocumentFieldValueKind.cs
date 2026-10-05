namespace PROManagementSystem.Application.Common.DocumentSchemas;

/// <summary>
/// The expected value type of a type-specific document field. Dates cover
/// birth/issue/expiry values; identifiers and text values use
/// <see cref="String"/>. The set is intentionally small: the schema layer
/// describes metadata, and ValueKind is metadata for future validation,
/// review, and AI extraction — it never converts or interprets values.
/// </summary>
public enum DocumentFieldValueKind
{
    String = 1,
    Date = 2,
    Number = 3,
    Boolean = 4
}