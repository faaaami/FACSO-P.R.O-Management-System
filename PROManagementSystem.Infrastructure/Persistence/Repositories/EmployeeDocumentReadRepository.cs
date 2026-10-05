using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;
using PROManagementSystem.Infrastructure.Persistence.Sql;

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

                {SqlSnippets.DocumentStatusNumericCase("expiry_date")}

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

    public async Task<GetEmployeeDocumentsResponseDto> GetByEmployeeIdForCompanyAsync(
        Guid clientCompanyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var whereCondition = """
            d.employee_id = @EmployeeId
            AND d.is_deleted = false
            """;

        var countSql = $"""
            SELECT COUNT(*)
            FROM documents d
            INNER JOIN employees e
                ON e.id = d.employee_id
            INNER JOIN client_entities ce
                ON ce.id = e.client_entity_id
            WHERE {whereCondition}
              AND ce.client_company_id = @ClientCompanyId;
            """;

        var dataSql = $"""
            SELECT
                d.id AS Id,
                d.employee_id AS EmployeeId,
                d.type AS Type,
                d.document_number AS DocumentNumber,
                d.issue_date AS IssueDate,
                d.expiry_date AS ExpiryDate,
                d.file_url AS FileUrl,
                d.file_name AS FileName,
                d.content_type AS ContentType,
                d.file_size AS FileSize,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted,
                d.created_at AS CreatedAt,
                d.updated_at AS UpdatedAt,

                {SqlSnippets.DocumentStatusNumericCase("d.expiry_date")}

            FROM documents d
            INNER JOIN employees e
                ON e.id = d.employee_id
            INNER JOIN client_entities ce
                ON ce.id = e.client_entity_id
            WHERE {whereCondition}
              AND ce.client_company_id = @ClientCompanyId
            ORDER BY d.created_at DESC;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            ClientCompanyId = clientCompanyId,
            EmployeeId = employeeId
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