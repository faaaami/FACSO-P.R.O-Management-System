using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class DocumentReadRepository
    : IDocumentReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DocumentReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetDocumentByIdResponseDto?> GetByIdAsync(
        Guid documentId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                id AS Id,
                client_entity_id AS ClientEntityId,
                employee_id AS EmployeeId,
                type AS Type,
                document_number AS DocumentNumber,
                issue_date AS IssueDate,
                expiry_date AS ExpiryDate,
                file_url AS FileUrl,
                file_name AS FileName,
                content_type AS ContentType,
                file_size AS FileSize,
                is_active AS IsActive,
                is_deleted AS IsDeleted,
                deleted_at AS DeletedAt,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,

                CASE
                    WHEN expiry_date IS NULL
                        THEN 1

                    WHEN expiry_date < CURRENT_TIMESTAMP
                        THEN 3

                    WHEN expiry_date <= CURRENT_TIMESTAMP
                        + INTERVAL '30 days'
                        THEN 2

                    ELSE 1
                END AS Status

            FROM documents
            WHERE id = @DocumentId
              AND (
                    @IncludeDeleted = true
                    OR is_deleted = false
                  );
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetDocumentByIdResponseDto>(
            new CommandDefinition(
                sql,
                new
                {
                    DocumentId = documentId,
                    IncludeDeleted = includeDeleted
                },
                cancellationToken: cancellationToken));
    }
}