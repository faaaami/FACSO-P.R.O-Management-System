using Dapper;
using Npgsql;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class ClientCompanyReadRepository
    : IClientCompanyReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ClientCompanyReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetClientCompaniesResponseDto> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!includeDeleted)
        {
            whereConditions.Add("is_deleted = false");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereConditions.Add(
                "(company_name ILIKE @Search " +
                "OR trade_license_number ILIKE @Search)");

            parameters.Add(
                "Search",
                $"%{search.Trim()}%");
        }

        var whereClause = whereConditions.Count > 0
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : string.Empty;

        var countSql = $"""
            SELECT COUNT(*)
            FROM client_companies
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                id AS Id,
                company_name AS CompanyName,
                trade_license_number AS TradeLicenseNumber,
                phone AS Phone,
                email AS Email,
                emirate AS Emirate,
                is_active AS IsActive,
                is_deleted AS IsDeleted,
                created_at AS CreatedAt
            FROM client_companies
            {whereClause}
            ORDER BY created_at DESC, id ASC
            {SqlSnippets.Pagination};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                countSql,
                parameters,
                cancellationToken: cancellationToken));

        var items = await connection.QueryAsync<ClientCompanyListItemDto>(
            new CommandDefinition(
                dataSql,
                parameters,
                cancellationToken: cancellationToken));

        return new GetClientCompaniesResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }



    public async Task<GetClientCompanyByIdResponseDto?> GetByIdAsync(
    Guid id,
    bool includeDeleted,
    CancellationToken cancellationToken)
    {
        var sql = """
        SELECT
            id AS Id,
            company_name AS CompanyName,
            trade_license_number AS TradeLicenseNumber,
            phone AS Phone,
            email AS Email,
            address AS Address,
            emirate AS Emirate,
            is_active AS IsActive,
            is_deleted AS IsDeleted,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
        FROM client_companies
        WHERE id = @Id
          AND (@IncludeDeleted = true OR is_deleted = false);
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetClientCompanyByIdResponseDto>(
            new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    IncludeDeleted = includeDeleted
                },
                cancellationToken: cancellationToken));
    }
}