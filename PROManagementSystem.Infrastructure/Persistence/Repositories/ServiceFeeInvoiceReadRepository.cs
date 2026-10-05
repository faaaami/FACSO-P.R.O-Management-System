using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class ServiceFeeInvoiceReadRepository
    : IServiceFeeInvoiceReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ServiceFeeInvoiceReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetServiceFeeInvoicesResponseDto> GetPagedAsync(
        Guid? clientCompanyId,
        Guid? renewalTaskId,
        ServiceFeeInvoiceStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            "si." + SqlColumnNames.ServiceFeeInvoiceIsDeleted + " = false"
        };

        var parameters = new DynamicParameters();

        if (clientCompanyId.HasValue)
        {
            whereConditions.Add(
                "si." + SqlColumnNames.ServiceFeeInvoiceClientCompanyId + " = @ClientCompanyId");

            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (renewalTaskId.HasValue)
        {
            whereConditions.Add(
                "si." + SqlColumnNames.ServiceFeeInvoiceRenewalTaskId + " = @RenewalTaskId");

            parameters.Add(
                "RenewalTaskId",
                renewalTaskId.Value);
        }

        if (status.HasValue)
        {
            whereConditions.Add(
                "si." + SqlColumnNames.ServiceFeeInvoiceStatus + " = @Status");

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
            FROM service_fee_invoices si
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                si.{SqlColumnNames.ServiceFeeInvoiceId} AS Id,
                si.{SqlColumnNames.ServiceFeeInvoiceClientCompanyId} AS ClientCompanyId,
                si.{SqlColumnNames.ServiceFeeInvoiceRenewalTaskId} AS RenewalTaskId,
                si.{SqlColumnNames.ServiceFeeInvoiceInvoiceNumber} AS InvoiceNumber,
                si.{SqlColumnNames.ServiceFeeInvoiceInvoiceDate} AS InvoiceDate,
                si.{SqlColumnNames.ServiceFeeInvoiceDueDate} AS DueDate,
                si.{SqlColumnNames.ServiceFeeInvoiceAmount} AS Amount,
                si.{SqlColumnNames.ServiceFeeInvoiceCurrency} AS Currency,
                si.{SqlColumnNames.ServiceFeeInvoiceStatus} AS Status,
                si.{SqlColumnNames.ServiceFeeInvoiceDescription} AS Description,
                si.{SqlColumnNames.ServiceFeeInvoicePaidAt} AS PaidAt,
                si.{SqlColumnNames.ServiceFeeInvoiceVoidedAt} AS VoidedAt,
                si.{SqlColumnNames.ServiceFeeInvoiceVoidReason} AS VoidReason,
                si.{SqlColumnNames.ServiceFeeInvoiceCreatedAt} AS CreatedAt
            FROM service_fee_invoices si
            {whereClause}
            ORDER BY si.{SqlColumnNames.ServiceFeeInvoiceCreatedAt} DESC
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
                GetServiceFeeInvoiceListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken:
                        cancellationToken));

        return new GetServiceFeeInvoicesResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<GetServiceFeeInvoiceByIdResponseDto?> GetByIdAsync(
        Guid id,
        Guid? clientCompanyId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        var conditions = new List<string>
        {
            "si." + SqlColumnNames.ServiceFeeInvoiceId + " = @Id",
            "si." + SqlColumnNames.ServiceFeeInvoiceIsDeleted + " = false"
        };

        if (clientCompanyId.HasValue)
        {
            conditions.Add(
                "si." + SqlColumnNames.ServiceFeeInvoiceClientCompanyId + " = @ClientCompanyId");
            parameters.Add("ClientCompanyId", clientCompanyId.Value);
        }

        var whereClause =
            string.Join(" AND ", conditions);

        var sql = $"""
            SELECT
                si.{SqlColumnNames.ServiceFeeInvoiceId} AS Id,
                si.{SqlColumnNames.ServiceFeeInvoiceClientCompanyId} AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                si.{SqlColumnNames.ServiceFeeInvoiceRenewalTaskId} AS RenewalTaskId,
                si.{SqlColumnNames.ServiceFeeInvoiceInvoiceNumber} AS InvoiceNumber,
                si.{SqlColumnNames.ServiceFeeInvoiceInvoiceDate} AS InvoiceDate,
                si.{SqlColumnNames.ServiceFeeInvoiceDueDate} AS DueDate,
                si.{SqlColumnNames.ServiceFeeInvoiceAmount} AS Amount,
                si.{SqlColumnNames.ServiceFeeInvoiceCurrency} AS Currency,
                si.{SqlColumnNames.ServiceFeeInvoiceStatus} AS Status,
                si.{SqlColumnNames.ServiceFeeInvoiceDescription} AS Description,
                si.{SqlColumnNames.ServiceFeeInvoicePaidAt} AS PaidAt,
                si.{SqlColumnNames.ServiceFeeInvoiceVoidedAt} AS VoidedAt,
                si.{SqlColumnNames.ServiceFeeInvoiceVoidReason} AS VoidReason,
                si.{SqlColumnNames.ServiceFeeInvoiceCreatedAt} AS CreatedAt,
                si.{SqlColumnNames.ServiceFeeInvoiceUpdatedAt} AS UpdatedAt,
                po.{SqlColumnNames.PaymentOrderId} AS PaymentOrderId,
                po.{SqlColumnNames.PaymentOrderStatus} AS PaymentOrderStatus,
                po.{SqlColumnNames.PaymentOrderRazorpayOrderId} AS RazorpayOrderId
            FROM service_fee_invoices si
            INNER JOIN client_companies cc
                ON cc.id = si.{SqlColumnNames.ServiceFeeInvoiceClientCompanyId}
            LEFT JOIN payment_orders po
                ON po.{SqlColumnNames.PaymentOrderServiceFeeInvoiceId} = si.{SqlColumnNames.ServiceFeeInvoiceId}
                AND po.{SqlColumnNames.PaymentOrderIsDeleted} = false
            WHERE {whereClause};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetServiceFeeInvoiceByIdResponseDto>(
            new CommandDefinition(
                sql,
                parameters,
                cancellationToken:
                    cancellationToken));
    }
}
