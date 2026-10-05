using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class PaymentOrderReadRepository
    : IPaymentOrderReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PaymentOrderReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetPaymentOrdersResponseDto?> GetListAsync(
        Guid? clientCompanyId,
        PaymentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var conditions = new List<string>
        {
            "po." + SqlColumnNames.PaymentOrderIsDeleted + " = false"
        };

        if (clientCompanyId.HasValue)
        {
            conditions.Add(
                "po." + SqlColumnNames.PaymentOrderClientCompanyId + " = @ClientCompanyId");
            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (status.HasValue)
        {
            conditions.Add("po." + SqlColumnNames.PaymentOrderStatus + " = @Status");
            parameters.Add("Status", (int)status.Value);
        }

        var whereClause =
            string.Join(" AND ", conditions);

        var countSql =
            $"""
            SELECT COUNT(*)
            FROM payment_orders po
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
                po.{SqlColumnNames.PaymentOrderId} AS Id,
                po.{SqlColumnNames.PaymentOrderInvoiceType} AS InvoiceType,
                po.{SqlColumnNames.PaymentOrderRetainerInvoiceId} AS RetainerInvoiceId,
                po.{SqlColumnNames.PaymentOrderServiceFeeInvoiceId} AS ServiceFeeInvoiceId,
                po.{SqlColumnNames.PaymentOrderClientCompanyId} AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                po.{SqlColumnNames.PaymentOrderAmount} AS Amount,
                po.{SqlColumnNames.PaymentOrderCurrency} AS Currency,
                po.{SqlColumnNames.PaymentOrderStatus} AS Status,
                po.{SqlColumnNames.PaymentOrderRazorpayOrderId} AS RazorpayOrderId,
                po.{SqlColumnNames.PaymentOrderPaidAt} AS PaidAt,
                po.{SqlColumnNames.PaymentOrderFailedAt} AS FailedAt,
                po.{SqlColumnNames.PaymentOrderCancelledAt} AS CancelledAt,
                po.{SqlColumnNames.PaymentOrderNotes} AS Notes,
                po.{SqlColumnNames.PaymentOrderCreatedAt} AS CreatedAt,
                po.{SqlColumnNames.PaymentOrderUpdatedAt} AS UpdatedAt
            FROM payment_orders po
            INNER JOIN client_companies cc
                ON cc.id = po.{SqlColumnNames.PaymentOrderClientCompanyId}
            WHERE {whereClause}
            ORDER BY po.{SqlColumnNames.PaymentOrderCreatedAt} DESC
            {SqlSnippets.Pagination};
            """;

        var items =
            (await connection.QueryAsync<PaymentOrderDto>(
                new CommandDefinition(
                    sql,
                    parameters,
                    cancellationToken:
                        cancellationToken)))
            .ToList();

        return new GetPaymentOrdersResponseDto(
            items,
            totalCount,
            page,
            pageSize);
    }

    public async Task<GetPaymentOrderByIdResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                po.{SqlColumnNames.PaymentOrderId} AS Id,
                po.{SqlColumnNames.PaymentOrderInvoiceType} AS InvoiceType,
                po.{SqlColumnNames.PaymentOrderRetainerInvoiceId} AS RetainerInvoiceId,
                po.{SqlColumnNames.PaymentOrderServiceFeeInvoiceId} AS ServiceFeeInvoiceId,
                po.{SqlColumnNames.PaymentOrderClientCompanyId} AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                po.{SqlColumnNames.PaymentOrderAmount} AS Amount,
                po.{SqlColumnNames.PaymentOrderCurrency} AS Currency,
                po.{SqlColumnNames.PaymentOrderStatus} AS Status,
                po.{SqlColumnNames.PaymentOrderRazorpayOrderId} AS RazorpayOrderId,
                po.{SqlColumnNames.PaymentOrderRazorpayPaymentId} AS RazorpayPaymentId,
                po.{SqlColumnNames.PaymentOrderPaidAt} AS PaidAt,
                po.{SqlColumnNames.PaymentOrderFailedAt} AS FailedAt,
                po.{SqlColumnNames.PaymentOrderCancelledAt} AS CancelledAt,
                po.{SqlColumnNames.PaymentOrderNotes} AS Notes,
                po.{SqlColumnNames.PaymentOrderCreatedAt} AS CreatedAt,
                po.{SqlColumnNames.PaymentOrderUpdatedAt} AS UpdatedAt
            FROM payment_orders po
            INNER JOIN client_companies cc
                ON cc.id = po.{SqlColumnNames.PaymentOrderClientCompanyId}
            WHERE po.{SqlColumnNames.PaymentOrderId} = @Id
              AND po.{SqlColumnNames.PaymentOrderIsDeleted} = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetPaymentOrderByIdResponseDto>(
            new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken:
                    cancellationToken));
    }
}
