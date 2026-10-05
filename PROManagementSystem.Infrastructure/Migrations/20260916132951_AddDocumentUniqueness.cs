using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Task 7B.6.4: database-level cross-extraction duplicate integrity.
            //
            // A plain four-column unique index is NOT sufficient: employee-owned
            // documents have a NULL client_entity_id and PostgreSQL's default
            // NULLS DISTINCT semantics would never treat two such rows as a
            // conflict. COALESCE collapses the exactly-one-owner CHECK into a
            // single non-null owner key. The partial predicate (is_deleted =
            // false) keeps soft-deleted documents out of the uniqueness rule so
            // recreation after soft delete remains allowed.
            //
            // The database constraint is the final integrity backstop; the
            // application-level DocumentDuplicateGuard remains the fast path.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_documents_active_owner_type_number"
                ON "documents"
                (
                    COALESCE("client_entity_id", "employee_id"),
                    "type",
                    "document_number"
                )
                WHERE "is_deleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "UX_documents_active_owner_type_number";
                """);
        }
    }
}
