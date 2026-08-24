using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class EmployeeDocumentReadRepository
    : IEmployeeDocumentReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public EmployeeDocumentReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetEmployeeDocumentsResponseDto> GetByEmployeeIdAsync(
        Guid employeeId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var whereCondition = """
            employee_id = @EmployeeId
            AND (@IncludeDeleted = true OR is_deleted = false)
            """;

        var countSql = $"""
            SELECT COUNT(*)
            FROM documents
            WHERE {whereCondition};
            """;

        var dataSql = $"""
            SELECT
                id AS Id,
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
            WHERE {whereCondition}
            ORDER BY created_at DESC;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            EmployeeId = employeeId,
            IncludeDeleted = includeDeleted
        };

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<
                EmployeeDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetEmployeeDocumentsResponseDto(
            items.ToList(),
            totalCount);
    }
}