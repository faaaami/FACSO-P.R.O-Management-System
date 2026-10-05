namespace PROManagementSystem.Application.Common.Interfaces;

public interface IDashboardReadRepository
{
    Task<DashboardSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken);

    Task<DashboardActivityFeedResponseDto> GetActivityFeedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<DashboardRevenueDto> GetRevenueAsync(
        string period,
        CancellationToken cancellationToken);

    Task<List<DashboardExpiryAlertDto>> GetExpiringDocumentsAsync(
        int days,
        CancellationToken cancellationToken);

    Task<List<DashboardExpiryAlertDto>> GetExpiringEmployeesAsync(
        int days,
        CancellationToken cancellationToken);
}

public sealed record DashboardSummaryDto(
    int TotalClients,
    int TotalEntities,
    int TotalEmployees,
    int ActiveRenewalTasks,
    int PendingRetainerInvoices,
    int PendingServiceFeeInvoices
);

public sealed record DashboardActivityFeedResponseDto(
    List<DashboardActivityFeedItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record DashboardActivityFeedItemDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityType,
    Guid EntityId,
    string? Description,
    DateTime CreatedAt
);

public sealed record DashboardRevenueDto(
    decimal TotalRevenue,
    decimal RetainerRevenue,
    decimal ServiceFeeRevenue,
    int InvoiceCount
);

public sealed record DashboardExpiryAlertDto(
    Guid Id,
    string DocumentNumber,
    string? FileName,
    DateTime ExpiryDate,
    int DaysUntilExpiry,
    string? EntityName,
    string? EmployeeName
);
