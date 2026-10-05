using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Sql;

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
        GovFeeDisbursementStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var conditions = new List<string>
        {
            "g." + SqlColumnNames.GovFeeDisbursementIsDeleted + " = false"
        };

        if (clientCompanyId.HasValue)
        {
            conditions.Add(
                "g." + SqlColumnNames.GovFeeDisbursementClientCompanyId + " = @ClientCompanyId");
            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (status.HasValue)
        {
            conditions.Add(
                "g." + SqlColumnNames.GovFeeDisbursementStatus + " = @Status");
            parameters.Add("Status", (int)status.Value);
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
                g.{SqlColumnNames.GovFeeDisbursementId} AS Id,
                g.{SqlColumnNames.GovFeeDisbursementClientCompanyId} AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                g.{SqlColumnNames.GovFeeDisbursementRenewalTaskId} AS RenewalTaskId,
                g.{SqlColumnNames.GovFeeDisbursementFeeDescription} AS FeeDescription,
                g.{SqlColumnNames.GovFeeDisbursementGovernmentReference} AS GovernmentReference,
                g.{SqlColumnNames.GovFeeDisbursementAmount} AS Amount,
                g.{SqlColumnNames.GovFeeDisbursementCurrency} AS Currency,
                g.{SqlColumnNames.GovFeeDisbursementStatus} AS Status,
                g.{SqlColumnNames.GovFeeDisbursementPaidByFirmAt} AS PaidByFirmAt,
                g.{SqlColumnNames.GovFeeDisbursementInvoicedAt} AS InvoicedAt,
                g.{SqlColumnNames.GovFeeDisbursementReimbursedAt} AS ReimbursedAt,
                g.{SqlColumnNames.GovFeeDisbursementNotes} AS Notes,
                g.{SqlColumnNames.GovFeeDisbursementCreatedAt} AS CreatedAt,
                g.{SqlColumnNames.GovFeeDisbursementUpdatedAt} AS UpdatedAt
            FROM gov_fee_disbursements g
            INNER JOIN client_companies cc
                ON cc.id = g.{SqlColumnNames.GovFeeDisbursementClientCompanyId}
            WHERE {whereClause}
            ORDER BY g.{SqlColumnNames.GovFeeDisbursementCreatedAt} DESC
            {SqlSnippets.Pagination};
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
        Guid? clientCompanyId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        var conditions = new List<string>
        {
            "g." + SqlColumnNames.GovFeeDisbursementId + " = @Id",
            "g." + SqlColumnNames.GovFeeDisbursementIsDeleted + " = false"
        };

        if (clientCompanyId.HasValue)
        {
            conditions.Add(
                "g." + SqlColumnNames.GovFeeDisbursementClientCompanyId + " = @ClientCompanyId");
            parameters.Add("ClientCompanyId", clientCompanyId.Value);
        }

        var whereClause =
            string.Join(" AND ", conditions);

        var sql = $"""
            SELECT
                g.{SqlColumnNames.GovFeeDisbursementId} AS Id,
                g.{SqlColumnNames.GovFeeDisbursementClientCompanyId} AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                g.{SqlColumnNames.GovFeeDisbursementRenewalTaskId} AS RenewalTaskId,
                g.{SqlColumnNames.GovFeeDisbursementFeeDescription} AS FeeDescription,
                g.{SqlColumnNames.GovFeeDisbursementGovernmentReference} AS GovernmentReference,
                g.{SqlColumnNames.GovFeeDisbursementAmount} AS Amount,
                g.{SqlColumnNames.GovFeeDisbursementCurrency} AS Currency,
                g.{SqlColumnNames.GovFeeDisbursementStatus} AS Status,
                g.{SqlColumnNames.GovFeeDisbursementPaidByFirmAt} AS PaidByFirmAt,
                g.{SqlColumnNames.GovFeeDisbursementInvoicedAt} AS InvoicedAt,
                g.{SqlColumnNames.GovFeeDisbursementReimbursedAt} AS ReimbursedAt,
                g.{SqlColumnNames.GovFeeDisbursementNotes} AS Notes,
                g.{SqlColumnNames.GovFeeDisbursementCreatedAt} AS CreatedAt,
                g.{SqlColumnNames.GovFeeDisbursementUpdatedAt} AS UpdatedAt
            FROM gov_fee_disbursements g
            INNER JOIN client_companies cc
                ON cc.id = g.{SqlColumnNames.GovFeeDisbursementClientCompanyId}
            WHERE {whereClause};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetGovFeeDisbursementByIdResponseDto>(
            new CommandDefinition(
                sql,
                parameters,
                cancellationToken:
                    cancellationToken));
    }
}
