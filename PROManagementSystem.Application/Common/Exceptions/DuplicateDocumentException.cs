namespace PROManagementSystem.Application.Common.Exceptions;

/// <summary>
/// Thrown when creating an active document would duplicate an existing
/// non-deleted document with the same owner, type and document number.
/// Mapped by the global exception middleware to a 409 with error code
/// DUPLICATE_DOCUMENT.
/// </summary>
public sealed class DuplicateDocumentException : ConflictException
{
    public DuplicateDocumentException(string message)
        : base(message)
    {
    }
}