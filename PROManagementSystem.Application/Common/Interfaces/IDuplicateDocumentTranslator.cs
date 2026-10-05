using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;

namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>
/// Translates a database-level unique-constraint violation into a domain
/// exception, when it originates from one of the specific unique constraints
/// this application reasons about:
/// <list type="bullet">
///   <item>
///     UX_documents_active_owner_type_number — the active-document uniqueness
///     rule — becomes a <see cref="DuplicateDocumentException"/>, which the
///     global exception middleware reports as 409 DUPLICATE_DOCUMENT.
///   </item>
///   <item>
///     IX_renewal_tasks_DocumentId — the filtered "one live renewal task per
///     document" index — becomes a plain <see cref="ConflictException"/>, which
///     the same middleware reports as 409 CONFLICT.
///   </item>
/// </list>
///
/// Returns null for every other violation, so unrelated unique-constraint errors
/// — a duplicate document version, or anything a future migration adds — keep
/// their original representation and are never reclassified as a domain
/// conflict.
///
/// Matching is on the exact PostgreSQL constraint name, never on a substring of
/// the message and never on the bare 23505 state, because those two are shared
/// by every unique constraint in the database.
/// </summary>
public interface IDuplicateDocumentTranslator
{
    ConflictException? Translate(DbUpdateException exception);
}