using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class GovFeeDisbursementReadRepository
    : IGovFeeDisbursementReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public GovFeeDisbursementReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetGovFeeDisbursementsResponseDto?> GetListAsync(
        Guid? clientCompanyId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var conditions = new List<string>
        {
            "g.\"IsDeleted\" = false"
        };

        if (clientCompanyId.HasValue)
        {
            conditions.Add(
                "g.\"ClientCompanyId\" = @ClientCompanyId");
            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add("g.\"Status\" = @Status");
            parameters.Add("Status", status);
        }

        var whereClause =
            string.Join(" AND ", conditions);

        var countSql =
            $"""
            SELECT COUNT(*)
            FROM gov_fee_disbursements g
            WHERE {whereClause}
            """;

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

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var sql =
            $"""
            SELECT
                g."Id" AS Id,
                g."ClientCompanyId" AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                g."RenewalTaskId" AS RenewalTaskId,
                g."FeeDescription" AS FeeDescription,
                g."GovernmentReference" AS GovernmentReference,
                g."Amount" AS Amount,
                g."Currency" AS Currency,
                g."Status" AS Status,
                g."PaidByFirmAt" AS PaidByFirmAt,
                g."InvoicedAt" AS InvoicedAt,
                g."ReimbursedAt" AS ReimbursedAt,
                g."Notes" AS Notes,
                g."CreatedAt" AS CreatedAt,
                g."UpdatedAt" AS UpdatedAt
            FROM gov_fee_disbursements g
            INNER JOIN client_companies cc
                ON cc.id = g."ClientCompanyId"
            WHERE {whereClause}
            ORDER BY g."CreatedAt" DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;
            """;

        var items =
            (await connection.QueryAsync<GovFeeDisbursementDto>(
                new CommandDefinition(
                    sql,
                    parameters,
                    cancellationToken:
                        cancellationToken)))
            .ToList();

        return new GetGovFeeDisbursementsResponseDto(
            items,
            totalCount,
            page,
            pageSize);
    }

    public async Task<GetGovFeeDisbursementByIdResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                g."Id" AS Id,
                g."ClientCompanyId" AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                g."RenewalTaskId" AS RenewalTaskId,
                g."FeeDescription" AS FeeDescription,
                g."GovernmentReference" AS GovernmentReference,
                g."Amount" AS Amount,
                g."Currency" AS Currency,
                g."Status" AS Status,
                g."PaidByFirmAt" AS PaidByFirmAt,
                g."InvoicedAt" AS InvoicedAt,
                g."ReimbursedAt" AS ReimbursedAt,
                g."Notes" AS Notes,
                g."CreatedAt" AS CreatedAt,
                g."UpdatedAt" AS UpdatedAt
            FROM gov_fee_disbursements g
            INNER JOIN client_companies cc
                ON cc.id = g."ClientCompanyId"
            WHERE g."Id" = @Id
              AND g."IsDeleted" = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetGovFeeDisbursementByIdResponseDto>(
            new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken:
                    cancellationToken));
    }
}
