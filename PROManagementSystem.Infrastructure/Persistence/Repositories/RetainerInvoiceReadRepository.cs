using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;
using PROManagementSystem.Domain.Enums;

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
            "ri.\"IsDeleted\" = false"
        };

        var parameters = new DynamicParameters();

        if (clientCompanyId.HasValue)
        {
            whereConditions.Add(
                "ri.\"ClientCompanyId\" = @ClientCompanyId");

            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        if (serviceContractId.HasValue)
        {
            whereConditions.Add(
                "ri.\"ServiceContractId\" = @ServiceContractId");

            parameters.Add(
                "ServiceContractId",
                serviceContractId.Value);
        }

        if (status.HasValue)
        {
            whereConditions.Add(
                "ri.\"Status\" = @Status");

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
                ri."Id" AS Id,
                ri."ClientCompanyId" AS ClientCompanyId,
                ri."ServiceContractId" AS ServiceContractId,
                ri."InvoiceNumber" AS InvoiceNumber,
                ri."InvoiceDate" AS InvoiceDate,
                ri."DueDate" AS DueDate,
                ri."PeriodStart" AS PeriodStart,
                ri."PeriodEnd" AS PeriodEnd,
                ri."Amount" AS Amount,
                ri."Currency" AS Currency,
                ri."Status" AS Status,
                ri."Notes" AS Notes,
                ri."PaidAt" AS PaidAt,
                ri."VoidedAt" AS VoidedAt,
                ri."VoidReason" AS VoidReason,
                ri."CreatedAt" AS CreatedAt
            FROM retainer_invoices ri
            {whereClause}
            ORDER BY ri."CreatedAt" DESC
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
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ri."Id" AS Id,
                ri."ClientCompanyId" AS ClientCompanyId,
                cc.company_name AS ClientCompanyName,
                ri."ServiceContractId" AS ServiceContractId,
                sc."ContractNumber" AS ContractNumber,
                ri."InvoiceNumber" AS InvoiceNumber,
                ri."InvoiceDate" AS InvoiceDate,
                ri."DueDate" AS DueDate,
                ri."PeriodStart" AS PeriodStart,
                ri."PeriodEnd" AS PeriodEnd,
                ri."Amount" AS Amount,
                ri."Currency" AS Currency,
                ri."Status" AS Status,
                ri."Notes" AS Notes,
                ri."PaidAt" AS PaidAt,
                ri."VoidedAt" AS VoidedAt,
                ri."VoidReason" AS VoidReason,
                ri."CreatedAt" AS CreatedAt,
                ri."UpdatedAt" AS UpdatedAt
            FROM retainer_invoices ri
            INNER JOIN client_companies cc
                ON cc.id = ri."ClientCompanyId"
            INNER JOIN service_contracts sc
                ON sc."Id" = ri."ServiceContractId"
            WHERE ri."Id" = @Id
              AND ri."IsDeleted" = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetRetainerInvoiceByIdResponseDto>(
            new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken:
                    cancellationToken));
    }
}
