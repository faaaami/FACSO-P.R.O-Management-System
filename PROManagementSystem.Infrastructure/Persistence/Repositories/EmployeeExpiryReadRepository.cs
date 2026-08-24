using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class EmployeeExpiryReadRepository
    : IEmployeeExpiryReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public EmployeeExpiryReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetExpiringEmployeesResponseDto>
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
            "e.is_deleted = false",
            "e.is_active = true",
            "d.is_deleted = false",
            "d.is_active = true",
            "d.expiry_date IS NOT NULL"
        };

        if (includeExpired)
        {
            whereConditions.Add(
                """
                d.expiry_date <= CURRENT_TIMESTAMP
                    + (@Days * INTERVAL '1 day')
                """);
        }
        else
        {
            whereConditions.Add(
                """
                d.expiry_date >= CURRENT_TIMESTAMP
                AND d.expiry_date <= CURRENT_TIMESTAMP
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
            FROM employees e
            INNER JOIN documents d
                ON d.employee_id = e.id
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                e.id AS EmployeeId,
                e.full_name AS FullName,
                e.client_entity_id AS ClientEntityId,

                d.id AS DocumentId,
                d.type AS DocumentType,
                d.document_number AS DocumentNumber,
                d.expiry_date AS ExpiryDate,

                FLOOR(
                    EXTRACT(
                        EPOCH FROM
                        (d.expiry_date - CURRENT_TIMESTAMP)
                    ) / 86400
                )::int AS DaysRemaining,

                CASE
                    WHEN d.expiry_date < CURRENT_TIMESTAMP
                        THEN 'Expired'

                    WHEN d.expiry_date <= CURRENT_TIMESTAMP
                        + INTERVAL '30 days'
                        THEN 'ExpiringSoon'

                    ELSE 'Active'
                END AS Status

            FROM employees e

            INNER JOIN documents d
                ON d.employee_id = e.id

            {whereClause}

            ORDER BY d.expiry_date ASC

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
                ExpiringEmployeeDocumentDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetExpiringEmployeesResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }
}