using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class RetainerInvoiceReadRepository
    : IRetainerInvoiceReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public RetainerInvoiceReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetRetainerInvoicesResponseDto> GetPagedAsync(
        Guid? clientCompanyId,
        Guid? serviceContractId,
        RetainerInvoiceStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            "ri." + SqlColumnNames.RetainerInvoiceIsDeleted + " = false"
        };

        var parameters = new DynamicParameters();

        if (clientCompanyId.HasValue)
        {
            whereConditions.Add(
                "ri." + SqlColumnNames.RetainerInvoiceClientCompanyId + " = @ClientCompanyId");

            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (serviceContractId.HasValue)
        {
            whereConditions.Add(
                "ri." + SqlColumnNames.RetainerInvoiceServiceContractId + " = @ServiceContractId");

            parameters.Add(
                "ServiceContractId",
                serviceContractId.Value);
        }

        if (status.HasValue)
        {
            whereConditions.Add(
                "ri." + SqlColumnNames.RetainerInvoiceStatus + " = @Status");

            parameters.Add(
                "Status",
                (int)status.Value);
        }

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM retainer_invoices ri
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                ri.{SqlColumnNames.RetainerInvoiceId} AS Id,
                ri.{SqlColumnNames.RetainerInvoiceClientCompanyId} AS ClientCompanyId,
                ri.{SqlColumnNames.RetainerInvoiceServiceContractId} AS ServiceContractId,
                ri.{SqlColumnNames.RetainerInvoiceInvoiceNumber} AS InvoiceNumber,
                ri.{SqlColumnNames.RetainerInvoiceInvoiceDate} AS InvoiceDate,
                ri.{SqlColumnNames.RetainerInvoiceDueDate} AS DueDate,
                ri.{SqlColumnNames.RetainerInvoicePeriodStart} AS PeriodStart,
                ri.{SqlColumnNames.RetainerInvoicePeriodEnd} AS PeriodEnd,
                ri.{SqlColumnNames.RetainerInvoiceAmount} AS Amount,
                ri.{SqlColumnNames.RetainerInvoiceCurrency} AS Currency,
                ri.{SqlColumnNames.RetainerInvoiceStatus} AS Status,
                ri.{SqlColumnNames.RetainerInvoiceNotes} AS Notes,
                ri.{SqlColumnNames.RetainerInvoicePaidAt} AS PaidAt,
                ri.{SqlColumnNames.RetainerInvoiceVoidedAt} AS VoidedAt,
                ri.{SqlColumnNames.RetainerInvoiceVoidReason} AS VoidReason,
                ri.{SqlColumnNames.RetainerInvoiceCreatedAt} AS CreatedAt
            FROM retainer_invoices ri
            {whereClause}
            ORDER BY ri.{SqlColumnNames.RetainerInvoiceCreatedAt} DESC
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
                    cancellationToken:
                        cancellationToken));

        var items =
            await connection.QueryAsync<
                GetRetainerInvoiceListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken:
                        cancellationToken));

        return new GetRetainerInvoicesResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<GetRetainerInvoiceByIdResponseDto?> GetByIdAsync(
        Guid id,
        Guid? clientCompanyId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        var conditions = new List<string>
        {
            "ri." + SqlColumnNames.RetainerInvoiceId + " = @Id",
            "ri." + SqlColumnNames.RetainerInvoiceIsDeleted + " = false"
        };

        if (clientCompanyId.HasValue)
        {
            conditions.Add(
                "ri." + SqlColumnNames.RetainerInvoiceClientCompanyId + " = @ClientCompanyId");
            parameters.Add("ClientCompanyId", clientCompanyId.Value);
        }

        var whereClause =
            string.Join(" AND ", conditions);

        var sql = $"""
            SELECT
                ri.{SqlColumnNames.RetainerInvoiceId} AS Id,
                ri.{SqlColumnNames.RetainerInvoiceClientCompanyId} AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                ri.{SqlColumnNames.RetainerInvoiceServiceContractId} AS ServiceContractId,
                sc.{SqlColumnNames.ServiceContractContractNumber} AS ContractNumber,
                ri.{SqlColumnNames.RetainerInvoiceInvoiceNumber} AS InvoiceNumber,
                ri.{SqlColumnNames.RetainerInvoiceInvoiceDate} AS InvoiceDate,
                ri.{SqlColumnNames.RetainerInvoiceDueDate} AS DueDate,
                ri.{SqlColumnNames.RetainerInvoicePeriodStart} AS PeriodStart,
                ri.{SqlColumnNames.RetainerInvoicePeriodEnd} AS PeriodEnd,
                ri.{SqlColumnNames.RetainerInvoiceAmount} AS Amount,
                ri.{SqlColumnNames.RetainerInvoiceCurrency} AS Currency,
                ri.{SqlColumnNames.RetainerInvoiceStatus} AS Status,
                ri.{SqlColumnNames.RetainerInvoiceNotes} AS Notes,
                ri.{SqlColumnNames.RetainerInvoicePaidAt} AS PaidAt,
                ri.{SqlColumnNames.RetainerInvoiceVoidedAt} AS VoidedAt,
                ri.{SqlColumnNames.RetainerInvoiceVoidReason} AS VoidReason,
                ri.{SqlColumnNames.RetainerInvoiceCreatedAt} AS CreatedAt,
                ri.{SqlColumnNames.RetainerInvoiceUpdatedAt} AS UpdatedAt,
                po.{SqlColumnNames.PaymentOrderId} AS PaymentOrderId,
                po.{SqlColumnNames.PaymentOrderStatus} AS PaymentOrderStatus,
                po.{SqlColumnNames.PaymentOrderRazorpayOrderId} AS RazorpayOrderId
            FROM retainer_invoices ri
            INNER JOIN client_companies cc
                ON cc.id = ri.{SqlColumnNames.RetainerInvoiceClientCompanyId}
            INNER JOIN service_contracts sc
                ON sc.{SqlColumnNames.ServiceContractId} = ri.{SqlColumnNames.RetainerInvoiceServiceContractId}
            -- An invoice can have many payment orders (cancelled, failed,
            -- paid); the detail shows the most recent one.
            LEFT JOIN LATERAL (
                SELECT *
                FROM payment_orders lpo
                WHERE lpo.{SqlColumnNames.PaymentOrderRetainerInvoiceId} = ri.{SqlColumnNames.RetainerInvoiceId}
                  AND lpo.{SqlColumnNames.PaymentOrderIsDeleted} = false
                ORDER BY lpo.{SqlColumnNames.PaymentOrderCreatedAt} DESC,
                         lpo.{SqlColumnNames.PaymentOrderId} DESC
                LIMIT 1
            ) po ON true
            WHERE {whereClause};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetRetainerInvoiceByIdResponseDto>(
            new CommandDefinition(
                sql,
                parameters,
                cancellationToken:
                    cancellationToken));
    }
}
