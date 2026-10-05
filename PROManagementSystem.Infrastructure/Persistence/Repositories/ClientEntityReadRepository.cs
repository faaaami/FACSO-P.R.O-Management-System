using Dapper;
using Npgsql;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityById;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class ClientEntityReadRepository
    : IClientEntityReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ClientEntityReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetClientEntitiesResponseDto> GetPagedAsync(
        Guid clientCompanyId,
        int page,
        int pageSize,
        string? search,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            "client_company_id = @ClientCompanyId"
        };

        var parameters = new DynamicParameters();

        parameters.Add(
            "ClientCompanyId",
            clientCompanyId);

        if (!includeDeleted)
        {
            whereConditions.Add(
                "is_deleted = false");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereConditions.Add(
                "(entity_name ILIKE @Search " +
                "OR trade_license_number ILIKE @Search)");

            parameters.Add(
                "Search",
                $"%{search.Trim()}%");
        }

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM client_entities
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                id AS Id,
                client_company_id AS ClientCompanyId,
                entity_name AS EntityName,
                trade_license_number AS TradeLicenseNumber,
                emirate AS Emirate,
                is_active AS IsActive,
                is_deleted AS IsDeleted,
                created_at AS CreatedAt
            FROM client_entities
            {whereClause}
            ORDER BY created_at DESC, id ASC
            {SqlSnippets.Pagination};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<GetClientEntityListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetClientEntitiesResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<GetClientEntityByIdResponseDto?> GetByIdAsync(
    Guid clientCompanyId,
    Guid entityId,
    bool includeDeleted,
    CancellationToken cancellationToken)
    {
        var sql = """
        SELECT
            id AS Id,
            client_company_id AS ClientCompanyId,
            entity_name AS EntityName,
            trade_license_number AS TradeLicenseNumber,
            emirate AS Emirate,
            is_active AS IsActive,
            is_deleted AS IsDeleted,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
        FROM client_entities
        WHERE id = @EntityId
          AND client_company_id = @ClientCompanyId
          AND (@IncludeDeleted = true OR is_deleted = false);
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetClientEntityByIdResponseDto>(
            new CommandDefinition(
                sql,
                new
                {
                    EntityId = entityId,
                    ClientCompanyId = clientCompanyId,
                    IncludeDeleted = includeDeleted
                },
                cancellationToken: cancellationToken));
    }
}