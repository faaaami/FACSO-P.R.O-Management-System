using Dapper;
using Npgsql;
using PROManagementSystem.Application.Clients.Queries.GetClientContacts;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class ClientContactReadRepository
    : IClientContactReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ClientContactReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetClientContactsResponseDto> GetPagedAsync(
        Guid clientCompanyId,
        int page,
        int pageSize,
        string? search,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var conditions = new List<string>
        {
            "client_company_id = @ClientCompanyId",
            "role = 'Client'"
        };

        var parameters = new DynamicParameters();

        parameters.Add(
            "ClientCompanyId",
            clientCompanyId);

        if (!includeDeleted)
        {
            conditions.Add("is_deleted = false");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add(
                "(full_name ILIKE @Search " +
                "OR email ILIKE @Search)");

            parameters.Add(
                "Search",
                $"%{search.Trim()}%");
        }

        var whereClause =
            "WHERE " + string.Join(" AND ", conditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM users
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                id AS Id,
                client_company_id AS ClientCompanyId,
                full_name AS FullName,
                email AS Email,
                phone AS Phone,
                role AS Role,
                is_active AS IsActive,
                is_approved AS IsApproved,
                is_deleted AS IsDeleted,
                created_at AS CreatedAt
            FROM users
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
            await connection.QueryAsync<GetClientContactListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetClientContactsResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }
}