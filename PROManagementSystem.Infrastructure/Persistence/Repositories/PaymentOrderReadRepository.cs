using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;

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
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var conditions = new List<string>
        {
            "po.\"IsDeleted\" = false"
        };

        if (clientCompanyId.HasValue)
        {
            conditions.Add(
                "po.\"ClientCompanyId\" = @ClientCompanyId");
            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add("po.\"Status\" = @Status");
            parameters.Add("Status", status);
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
                po."Id" AS Id,
                po."InvoiceType" AS InvoiceType,
                po."RetainerInvoiceId" AS RetainerInvoiceId,
                po."ServiceFeeInvoiceId" AS ServiceFeeInvoiceId,
                po."ClientCompanyId" AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                po."Amount" AS Amount,
                po."Currency" AS Currency,
                po."Status" AS Status,
                po."RazorpayOrderId" AS RazorpayOrderId,
                po."PaidAt" AS PaidAt,
                po."FailedAt" AS FailedAt,
                po."CancelledAt" AS CancelledAt,
                po."Notes" AS Notes,
                po."CreatedAt" AS CreatedAt,
                po."UpdatedAt" AS UpdatedAt
            FROM payment_orders po
            INNER JOIN client_companies cc
                ON cc.id = po."ClientCompanyId"
            WHERE {whereClause}
            ORDER BY po."CreatedAt" DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;
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
        const string sql = """
            SELECT
                po."Id" AS Id,
                po."InvoiceType" AS InvoiceType,
                po."RetainerInvoiceId" AS RetainerInvoiceId,
                po."ServiceFeeInvoiceId" AS ServiceFeeInvoiceId,
                po."ClientCompanyId" AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                po."Amount" AS Amount,
                po."Currency" AS Currency,
                po."Status" AS Status,
                po."RazorpayOrderId" AS RazorpayOrderId,
                po."RazorpayPaymentId" AS RazorpayPaymentId,
                po."PaidAt" AS PaidAt,
                po."FailedAt" AS FailedAt,
                po."CancelledAt" AS CancelledAt,
                po."Notes" AS Notes,
                po."CreatedAt" AS CreatedAt,
                po."UpdatedAt" AS UpdatedAt
            FROM payment_orders po
            INNER JOIN client_companies cc
                ON cc.id = po."ClientCompanyId"
            WHERE po."Id" = @Id
              AND po."IsDeleted" = false;
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
