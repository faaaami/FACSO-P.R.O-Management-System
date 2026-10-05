using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Helpers;

/// <summary>
/// Guards against creating a document that would duplicate an existing
/// non-deleted document with the same owner, type and document number.
/// Follows the project's DocumentNumber convention: the value is
/// outer-trimmed (as every write path stores it) and compared ordinal,
/// case-sensitively. Soft-deleted documents are ignored. Update paths pass
/// <paramref name="excludeDocumentId"/> so the document being updated is not
/// matched against itself.
/// </summary>
public static class DocumentDuplicateGuard
{
    public static async Task EnsureNotDuplicateAsync(
        IApplicationDbContext context,
        Guid? employeeId,
        Guid? clientEntityId,
        DocumentType type,
        string documentNumber,
        CancellationToken cancellationToken,
        Guid? excludeDocumentId = null)
    {
        var normalized = documentNumber.Trim();

        var duplicateExists =
            await context.Documents.AnyAsync(
                x => !x.IsDeleted
                     && x.Id != excludeDocumentId
                     && x.EmployeeId == employeeId
                     && x.ClientEntityId == clientEntityId
                     && x.Type == type
                     && x.DocumentNumber == normalized,
                cancellationToken);

        if (duplicateExists)
        {
            throw new DuplicateDocumentException(
                $"An active document with type '{type}' and document " +
                $"number '{normalized}' already exists for this owner.");
        }
    }
}