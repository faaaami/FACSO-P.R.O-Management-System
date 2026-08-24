using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;
using PROManagementSystem.Domain.Enums;

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
            "si.\"IsDeleted\" = false"
        };

        var parameters = new DynamicParameters();

        if (clientCompanyId.HasValue)
        {
            whereConditions.Add(
                "si.\"ClientCompanyId\" = @ClientCompanyId");

            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (renewalTaskId.HasValue)
        {
            whereConditions.Add(
                "si.\"RenewalTaskId\" = @RenewalTaskId");

            parameters.Add(
                "RenewalTaskId",
                renewalTaskId.Value);
        }

        if (status.HasValue)
        {
            whereConditions.Add(
                "si.\"Status\" = @Status");

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
                si."Id" AS Id,
                si."ClientCompanyId" AS ClientCompanyId,
                si."RenewalTaskId" AS RenewalTaskId,
                si."InvoiceNumber" AS InvoiceNumber,
                si."InvoiceDate" AS InvoiceDate,
                si."DueDate" AS DueDate,
                si."Amount" AS Amount,
                si."Currency" AS Currency,
                si."Status" AS Status,
                si."Description" AS Description,
                si."PaidAt" AS PaidAt,
                si."VoidedAt" AS VoidedAt,
                si."VoidReason" AS VoidReason,
                si."CreatedAt" AS CreatedAt
            FROM service_fee_invoices si
            {whereClause}
            ORDER BY si."CreatedAt" DESC
            OFFSET @Offset
            LIMIT @PageSize;
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
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                si."Id" AS Id,
                si."ClientCompanyId" AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                si."RenewalTaskId" AS RenewalTaskId,
                si."InvoiceNumber" AS InvoiceNumber,
                si."InvoiceDate" AS InvoiceDate,
                si."DueDate" AS DueDate,
                si."Amount" AS Amount,
                si."Currency" AS Currency,
                si."Status" AS Status,
                si."Description" AS Description,
                si."PaidAt" AS PaidAt,
                si."VoidedAt" AS VoidedAt,
                si."VoidReason" AS VoidReason,
                si."CreatedAt" AS CreatedAt,
                si."UpdatedAt" AS UpdatedAt
            FROM service_fee_invoices si
            INNER JOIN client_companies cc
                ON cc.id = si."ClientCompanyId"
            WHERE si."Id" = @Id
              AND si."IsDeleted" = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetServiceFeeInvoiceByIdResponseDto>(
            new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken:
                    cancellationToken));
    }
}
