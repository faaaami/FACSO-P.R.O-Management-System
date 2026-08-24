namespace PROManagementSystem.Application.Common.Interfaces;

public interface IClientServiceReportReadRepository
{
    Task<ClientServiceReportDto> GetReportAsync(
        Guid clientCompanyId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken);
}

public sealed record ClientServiceReportDto(
    ClientServiceReportClientDto Client,
    List<ClientServiceReportTaskDto> Tasks,
    List<ClientServiceReportInvoiceDto> Invoices,
    ClientServiceReportSummaryDto Summary
);

public sealed record ClientServiceReportClientDto(
    Guid Id,
    string CompanyName
);

public sealed record ClientServiceReportTaskDto(
    Guid Id,
    string? DocumentNumber,
    string Status,
    DateTime? CompletedAt,
    string? AssignedStaffName
);

public sealed record ClientServiceReportInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    string Type,
    decimal Amount,
    string Status,
    DateTime CreatedAt
);

public sealed record ClientServiceReportSummaryDto(
    int TotalTasks,
    int CompletedTasks,
    int PendingTasks,
    decimal TotalInvoiced,
    decimal TotalPaid,
    int TotalInvoices
);
