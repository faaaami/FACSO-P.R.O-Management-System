using Microsoft.EntityFrameworkCore;
using Npgsql;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Common;

/// <summary>
/// Translates PostgreSQL unique-violation errors into the domain exception that
/// matches the constraint which was actually violated. Only exact constraint
/// names are matched: unrelated unique violations (for example a duplicate
/// document version) are left untouched, so a genuine database error is never
/// reclassified as a domain conflict.
/// </summary>
public sealed class NpgsqlDuplicateDocumentTranslator
    : IDuplicateDocumentTranslator
{
    /// <summary>
    /// Active-document uniqueness, from the AddDocumentUniqueness migration.
    /// </summary>
    public const string DocumentUniquenessConstraint =
        "UX_documents_active_owner_type_number";

    /// <summary>
    /// The filtered unique index on renewal_tasks.DocumentId, declared in
    /// RenewalTaskConfiguration as
    /// <c>IsDeleted = false AND Status != 6</c> — one live renewal task per
    /// document.
    ///
    /// This is the constraint a concurrent create loses on. Both application
    /// writers of a renewal task check for an existing active task first, but
    /// that check is a read followed by an insert, so two requests that read
    /// before either wrote both proceed and PostgreSQL refuses the second. The
    /// job path already treats this as an idempotent no-op
    /// (RenewalTaskCreationService); an API caller gets a 409 instead, which is
    /// the same outcome the losing caller would have received had it seen the
    /// winner's row.
    /// </summary>
    public const string RenewalTaskDocumentUniquenessConstraint =
        "IX_renewal_tasks_DocumentId";

    public ConflictException? Translate(
        DbUpdateException exception)
    {
        var violation = FindPostgresException(exception);

        if (violation is null)
        {
            return null;
        }

        return violation.ConstraintName switch
        {
            DocumentUniquenessConstraint
                => new DuplicateDocumentException(
                    "An active document with the same owner, type and " +
                    "number already exists."),

            RenewalTaskDocumentUniquenessConstraint
                => new ConflictException(
                    "An active renewal task already exists for this " +
                    "document."),

            _ => null
        };
    }

    private static PostgresException? FindPostgresException(
        Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState:
                        PostgresErrorCodes.UniqueViolation
                } postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }
}