using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class DocumentExpiryReadRepository
    : IDocumentExpiryReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DocumentExpiryReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetExpiringDocumentsResponseDto>
        GetExpiringAsync(
            int days,
            int page,
            int pageSize,
            bool includeExpired,
            CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            "is_deleted = false",
            "expiry_date IS NOT NULL"
        };

        if (includeExpired)
        {
            whereConditions.Add(
                """
                expiry_date <= CURRENT_TIMESTAMP
                + (@Days * INTERVAL '1 day')
                """);
        }
        else
        {
            whereConditions.Add(
                """
                expiry_date >= CURRENT_TIMESTAMP
                AND expiry_date <= CURRENT_TIMESTAMP
                    + (@Days * INTERVAL '1 day')
                """);
        }

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM documents
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                id AS Id,
                client_entity_id AS ClientEntityId,
                employee_id AS EmployeeId,
                type AS Type,
                document_number AS DocumentNumber,
                issue_date AS IssueDate,
                expiry_date AS ExpiryDate,
                file_name AS FileName,
                is_active AS IsActive,
                is_deleted AS IsDeleted,

                FLOOR(
                    EXTRACT(
                        EPOCH FROM
                        (expiry_date - CURRENT_TIMESTAMP)
                    ) / 86400
                )::int AS DaysRemaining,

                CASE
                    WHEN expiry_date < CURRENT_TIMESTAMP
                        THEN 'Expired'

                    WHEN expiry_date <= CURRENT_TIMESTAMP
                        + INTERVAL '90 days'
                        THEN 'ExpiringSoon'

                    ELSE 'Active'
                END AS Status

            FROM documents
            {whereClause}

            ORDER BY expiry_date ASC

            OFFSET @Offset
            LIMIT @PageSize;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            Days = days,
            Offset = offset,
            PageSize = pageSize
        };

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<
                ExpiringDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetExpiringDocumentsResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }
}