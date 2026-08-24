using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class ClientServiceReportReadRepository
    : IClientServiceReportReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ClientServiceReportReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<ClientServiceReportDto> GetReportAsync(
        Guid clientCompanyId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var clientSql = """
            SELECT id AS Id, company_name AS CompanyName
            FROM client_companies
            WHERE id = @ClientId AND is_deleted = false;
            """;

        var client = await connection.QuerySingleOrDefaultAsync<ClientServiceReportClientDto>(
            new CommandDefinition(clientSql,
                new { ClientId = clientCompanyId },
                cancellationToken: cancellationToken));

        if (client is null)
        {
            return new ClientServiceReportDto(
                new ClientServiceReportClientDto(clientCompanyId, "Unknown"),
                new List<ClientServiceReportTaskDto>(),
                new List<ClientServiceReportInvoiceDto>(),
                new ClientServiceReportSummaryDto(0, 0, 0, 0, 0, 0));
        }

        var tasksSql = """
            SELECT
                rt."Id",
                d.document_number AS "DocumentNumber",
                CASE rt."Status"
                    WHEN 1 THEN 'Submitted' WHEN 2 THEN 'FeePaid'
                    WHEN 3 THEN 'AwaitingApproval' WHEN 4 THEN 'Blocked'
                    WHEN 5 THEN 'Approved' WHEN 6 THEN 'Updated'
                    ELSE rt."Status"::text
                END AS "Status",
                rt."CompletedAt",
                u.full_name AS "AssignedStaffName"
            FROM renewal_tasks rt
            LEFT JOIN documents d ON d.id = rt."DocumentId"
            LEFT JOIN users u ON u.id = rt."AssignedStaffId"
            WHERE rt."ClientCompanyId" = @ClientId AND rt."IsDeleted" = false
            ORDER BY rt."CreatedAt" DESC;
            """;

        var tasks = (await connection.QueryAsync<ClientServiceReportTaskDto>(
            new CommandDefinition(tasksSql,
                new { ClientId = clientCompanyId },
                cancellationToken: cancellationToken)))
            .ToList();

        var invoicesSql = """
            SELECT ri."Id", ri."InvoiceNumber", 'Retainer' AS "Type",
                   ri."Amount",
                   CASE ri."Status" WHEN 1 THEN 'Pending' WHEN 2 THEN 'Paid' WHEN 3 THEN 'Void' ELSE ri."Status"::text END AS "Status",
                   ri."CreatedAt"
            FROM retainer_invoices ri
            WHERE ri."ClientCompanyId" = @ClientId AND ri."IsDeleted" = false
            UNION ALL
            SELECT sfi."Id", sfi."InvoiceNumber", 'ServiceFee',
                   sfi."Amount",
                   CASE sfi."Status" WHEN 1 THEN 'Pending' WHEN 2 THEN 'Paid' WHEN 3 THEN 'Void' ELSE sfi."Status"::text END,
                   sfi."CreatedAt"
            FROM service_fee_invoices sfi
            WHERE sfi."ClientCompanyId" = @ClientId AND sfi."IsDeleted" = false
            ORDER BY "CreatedAt" DESC;
            """;

        var invoices = (await connection.QueryAsync<ClientServiceReportInvoiceDto>(
            new CommandDefinition(invoicesSql,
                new { ClientId = clientCompanyId },
                cancellationToken: cancellationToken)))
            .ToList();

        var completedTasks = tasks.Count(t => t.Status == "Updated");
        var pendingTasks = tasks.Count - completedTasks;
        var totalInvoiced = invoices.Sum(i => i.Amount);
        var totalPaid = invoices
            .Where(i => i.Status == "Paid")
            .Sum(i => i.Amount);

        var summary = new ClientServiceReportSummaryDto(
            tasks.Count,
            completedTasks,
            pendingTasks,
            totalInvoiced,
            totalPaid,
            invoices.Count);

        return new ClientServiceReportDto(
            client,
            tasks,
            invoices,
            summary);
    }
}
