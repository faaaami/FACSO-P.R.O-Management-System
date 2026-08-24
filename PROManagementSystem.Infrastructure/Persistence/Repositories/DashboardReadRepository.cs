using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class DashboardReadRepository
    : IDashboardReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DashboardReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var sql = """
            SELECT
                (SELECT COUNT(*)::int FROM client_companies WHERE is_deleted = false) AS "TotalClients",
                (SELECT COUNT(*)::int FROM client_entities WHERE is_deleted = false) AS "TotalEntities",
                (SELECT COUNT(*)::int FROM employees WHERE is_deleted = false) AS "TotalEmployees",
                (SELECT COUNT(*)::int FROM renewal_tasks WHERE "IsDeleted" = false AND "Status" IN (1,2,3,5)) AS "ActiveRenewalTasks",
                (SELECT COUNT(*)::int FROM retainer_invoices WHERE "IsDeleted" = false AND "Status" = 0) AS "PendingRetainerInvoices",
                (SELECT COUNT(*)::int FROM service_fee_invoices WHERE "IsDeleted" = false AND "Status" = 0) AS "PendingServiceFeeInvoices";
            """;

        return await connection.QuerySingleAsync<DashboardSummaryDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<DashboardStaffWorkloadDto> GetStaffWorkloadAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var sql = """
            SELECT "StaffId", "StaffName", "PendingTasks", "InProgressTasks", "BlockedTasks"
            FROM (
                SELECT
                    u.id AS "StaffId",
                    u.full_name AS "StaffName",
                    COUNT(CASE WHEN rt."Status" IN (1,2,3) THEN 1 END)::int AS "PendingTasks",
                    COUNT(CASE WHEN rt."Status" = 5 THEN 1 END)::int AS "InProgressTasks",
                    COUNT(CASE WHEN rt."Status" = 6 THEN 1 END)::int AS "BlockedTasks"
                FROM users u
                INNER JOIN renewal_tasks rt
                    ON rt."AssignedStaffId" = u.id AND rt."IsDeleted" = false
                WHERE u.is_deleted = false AND u.role = 'Agent'
                GROUP BY u.id, u.full_name
            ) sub
            ORDER BY ("PendingTasks" + "InProgressTasks" + "BlockedTasks") DESC;
            """;

        var items = (await connection.QueryAsync<DashboardStaffWorkloadItemDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)))
            .ToList();

        return new DashboardStaffWorkloadDto(items);
    }

    public async Task<DashboardActivityFeedResponseDto> GetActivityFeedAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var countSql = "SELECT COUNT(*) FROM audit_logs al WHERE al.is_deleted = false;";

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, cancellationToken: cancellationToken));

        var offset = (page - 1) * pageSize;

        var dataSql = $"""
            SELECT
                al.id AS "Id", al.user_id AS "UserId", u.full_name AS "UserName",
                al.action AS "Action", al.entity_type AS "EntityType",
                al.entity_id AS "EntityId", al.description AS "Description",
                al.created_at AS "CreatedAt"
            FROM audit_logs al
            LEFT JOIN users u ON u.id = al.user_id
            WHERE al.is_deleted = false
            ORDER BY al.created_at DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var items = (await connection.QueryAsync<DashboardActivityFeedItemDto>(
            new CommandDefinition(dataSql,
                new { Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken)))
            .ToList();

        return new DashboardActivityFeedResponseDto(items, totalCount, page, pageSize);
    }

    public async Task<DashboardRevenueDto> GetRevenueAsync(
        string period, CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var periodStart = period == "week"
            ? DateTime.UtcNow.AddDays(-7)
            : DateTime.UtcNow.AddMonths(-1);

        var sql = """
            SELECT
                COALESCE(SUM("Amount"), 0) AS "TotalRevenue",
                COALESCE(SUM(CASE WHEN src = 'retainer' THEN "Amount" ELSE 0 END), 0) AS "RetainerRevenue",
                COALESCE(SUM(CASE WHEN src = 'servicefee' THEN "Amount" ELSE 0 END), 0) AS "ServiceFeeRevenue",
                COUNT(*)::int AS "InvoiceCount"
            FROM (
                SELECT "Amount", 'retainer' AS src FROM retainer_invoices
                WHERE "IsDeleted" = false AND "Status" = 2 AND "PaidAt" >= @PeriodStart
                UNION ALL
                SELECT "Amount", 'servicefee' AS src FROM service_fee_invoices
                WHERE "IsDeleted" = false AND "Status" = 2 AND "PaidAt" >= @PeriodStart
            ) combined;
            """;

        return await connection.QuerySingleAsync<DashboardRevenueDto>(
            new CommandDefinition(sql,
                new { PeriodStart = periodStart },
                cancellationToken: cancellationToken));
    }

    public async Task<List<DashboardExpiryAlertDto>> GetExpiringDocumentsAsync(
        int days, CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var sql = """
            SELECT
                d.id AS "Id", d.document_number AS "DocumentNumber",
                d.file_name AS "FileName", d.expiry_date AS "ExpiryDate",
                EXTRACT(DAY FROM d.expiry_date - NOW())::int AS "DaysUntilExpiry",
                ce.entity_name AS "EntityName", NULL::text AS "EmployeeName"
            FROM documents d
            LEFT JOIN client_entities ce ON ce.id = d.client_entity_id
            WHERE d.is_deleted = false AND d.is_active = true
              AND d.expiry_date IS NOT NULL
              AND d.expiry_date >= NOW()
              AND d.expiry_date <= NOW() + (@Days || ' days')::interval
            ORDER BY d.expiry_date ASC LIMIT 20;
            """;

        return (await connection.QueryAsync<DashboardExpiryAlertDto>(
            new CommandDefinition(sql, new { Days = days },
                cancellationToken: cancellationToken)))
            .ToList();
    }

    public async Task<List<DashboardExpiryAlertDto>> GetExpiringEmployeesAsync(
        int days, CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var sql = """
            SELECT
                d.id AS "Id", d.document_number AS "DocumentNumber",
                d.file_name AS "FileName", d.expiry_date AS "ExpiryDate",
                EXTRACT(DAY FROM d.expiry_date - NOW())::int AS "DaysUntilExpiry",
                NULL::text AS "EntityName", e.full_name AS "EmployeeName"
            FROM documents d
            INNER JOIN employees e ON e.id = d.employee_id
            WHERE d.is_deleted = false AND d.is_active = true
              AND d.expiry_date IS NOT NULL
              AND d.expiry_date >= NOW()
              AND d.expiry_date <= NOW() + (@Days || ' days')::interval
            ORDER BY d.expiry_date ASC LIMIT 20;
            """;

        return (await connection.QueryAsync<DashboardExpiryAlertDto>(
            new CommandDefinition(sql, new { Days = days },
                cancellationToken: cancellationToken)))
            .ToList();
    }
}
