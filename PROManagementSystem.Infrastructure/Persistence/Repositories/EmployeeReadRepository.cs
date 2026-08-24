using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class EmployeeReadRepository
    : IEmployeeReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public EmployeeReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetEmployeesResponseDto> GetPagedAsync(
        Guid? clientId,
        Guid? entityId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            "e.is_deleted = false"
        };

        var parameters = new DynamicParameters();

        // Filter by ClientCompany
        if (clientId.HasValue)
        {
            whereConditions.Add(
                "ce.client_company_id = @ClientId");

            parameters.Add(
                "ClientId",
                clientId.Value);
        }

        // Filter by ClientEntity
        if (entityId.HasValue)
        {
            whereConditions.Add(
                "e.client_entity_id = @EntityId");

            parameters.Add(
                "EntityId",
                entityId.Value);
        }

        // Filter by Active/Inactive
        if (isActive.HasValue)
        {
            whereConditions.Add(
                "e.is_active = @IsActive");

            parameters.Add(
                "IsActive",
                isActive.Value);
        }

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM employees e
            INNER JOIN client_entities ce
                ON ce.id = e.client_entity_id
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                e.id AS Id,
                e.client_entity_id AS ClientEntityId,
                ce.entity_name AS EntityName,
                e.full_name AS FullName,
                e.passport_number AS PassportNumber,
                e.nationality AS Nationality,
                e.job_title AS JobTitle,
                e.hire_date AS HireDate,
                e.is_active AS IsActive,
                e.is_deleted AS IsDeleted,
                e.created_at AS CreatedAt
            FROM employees e
            INNER JOIN client_entities ce
                ON ce.id = e.client_entity_id
            {whereClause}
            ORDER BY e.created_at DESC
            OFFSET @Offset
            LIMIT @PageSize;
            """;

        parameters.Add(
            "Offset",
            offset);

        parameters.Add(
            "PageSize",
            pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken:
                        cancellationToken));

        var items =
            await connection.QueryAsync<
                GetEmployeeListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken:
                        cancellationToken));

        return new GetEmployeesResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<GetEmployeeByIdResponseDto?> GetByIdAsync(
    Guid employeeId,
    bool includeDeleted,
    CancellationToken cancellationToken)
    {
        var sql = """
        SELECT
            e.id AS Id,
            e.client_entity_id AS ClientEntityId,
            ce.client_company_id AS ClientCompanyId,
            ce.entity_name AS EntityName,
            e.full_name AS FullName,
            e.passport_number AS PassportNumber,
            e.date_of_birth AS DateOfBirth,
            e.nationality AS Nationality,
            e.job_title AS JobTitle,
            e.hire_date AS HireDate,
            e.is_active AS IsActive,
            e.is_deleted AS IsDeleted,
            e.created_at AS CreatedAt,
            e.updated_at AS UpdatedAt
        FROM employees e
        INNER JOIN client_entities ce
            ON ce.id = e.client_entity_id
        WHERE e.id = @EmployeeId
          AND (
              @IncludeDeleted = true
              OR e.is_deleted = false
          );
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetEmployeeByIdResponseDto>(
            new CommandDefinition(
                sql,
                new
                {
                    EmployeeId = employeeId,
                    IncludeDeleted = includeDeleted
                },
                cancellationToken: cancellationToken));
    }
}