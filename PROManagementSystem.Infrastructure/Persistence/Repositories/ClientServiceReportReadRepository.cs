using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Persistence.Sql;

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

        // The client row is also the existence check: the query already filters
        // `is_deleted = false`, so a null here means the company is EITHER
        // absent or soft-deleted.
        //
        // Previously this returned a fabricated report (company name "Unknown",
        // empty lists, zeroed summary) with HTTP 200, which silently masked a
        // bad id and produced a real PDF headed "Service Report — Unknown".
        // Throwing instead routes through GlobalExceptionMiddleware, whose
        // KeyNotFoundException branch yields 404 for BOTH the JSON and the PDF
        // endpoint.
        //
        // Deliberately NOT touched: the `from`/`to` arguments are still
        // accepted and validated but not applied to any query. That is a
        // separate, decision-blocked defect.
        if (client is null)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        var tasksSql = $"""
            SELECT
                rt.{SqlColumnNames.RenewalTaskId},
                d.document_number AS "DocumentNumber",
                CASE rt.{SqlColumnNames.RenewalTaskStatus}
                    WHEN 1 THEN 'Submitted' WHEN 2 THEN 'FeePaid'
                    WHEN 3 THEN 'AwaitingApproval' WHEN 4 THEN 'Blocked'
                    WHEN 5 THEN 'Approved' WHEN 6 THEN 'Updated'
                    ELSE rt.{SqlColumnNames.RenewalTaskStatus}::text
                END AS "Status",
                rt.{SqlColumnNames.RenewalTaskCompletedAt},
                u.full_name AS "AssignedStaffName"
            FROM renewal_tasks rt
            LEFT JOIN documents d ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}
            LEFT JOIN users u ON u.id = rt.{SqlColumnNames.RenewalTaskAssignedStaffId}
            WHERE rt.{SqlColumnNames.RenewalTaskClientCompanyId} = @ClientId AND rt.{SqlColumnNames.RenewalTaskIsDeleted} = false
            ORDER BY rt.{SqlColumnNames.RenewalTaskCreatedAt} DESC;
            """;

        var tasks = (await connection.QueryAsync<ClientServiceReportTaskDto>(
            new CommandDefinition(tasksSql,
                new { ClientId = clientCompanyId },
                cancellationToken: cancellationToken)))
            .ToList();

        var invoicesSql = $"""
            SELECT ri.{SqlColumnNames.RetainerInvoiceId}, ri.{SqlColumnNames.RetainerInvoiceInvoiceNumber}, 'Retainer' AS "Type",
                   ri.{SqlColumnNames.RetainerInvoiceAmount},
                   CASE ri.{SqlColumnNames.RetainerInvoiceStatus} WHEN 1 THEN 'Pending' WHEN 2 THEN 'Paid' WHEN 3 THEN 'Void' ELSE ri.{SqlColumnNames.RetainerInvoiceStatus}::text END AS "Status",
                   ri.{SqlColumnNames.RetainerInvoiceCreatedAt}
            FROM retainer_invoices ri
            WHERE ri.{SqlColumnNames.RetainerInvoiceClientCompanyId} = @ClientId AND ri.{SqlColumnNames.RetainerInvoiceIsDeleted} = false
            UNION ALL
            SELECT sfi.{SqlColumnNames.ServiceFeeInvoiceId}, sfi.{SqlColumnNames.ServiceFeeInvoiceInvoiceNumber}, 'ServiceFee',
                   sfi.{SqlColumnNames.ServiceFeeInvoiceAmount},
                   CASE sfi.{SqlColumnNames.ServiceFeeInvoiceStatus} WHEN 1 THEN 'Pending' WHEN 2 THEN 'Paid' WHEN 3 THEN 'Void' ELSE sfi.{SqlColumnNames.ServiceFeeInvoiceStatus}::text END,
                   sfi.{SqlColumnNames.ServiceFeeInvoiceCreatedAt}
            FROM service_fee_invoices sfi
            WHERE sfi.{SqlColumnNames.ServiceFeeInvoiceClientCompanyId} = @ClientId AND sfi.{SqlColumnNames.ServiceFeeInvoiceIsDeleted} = false
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
