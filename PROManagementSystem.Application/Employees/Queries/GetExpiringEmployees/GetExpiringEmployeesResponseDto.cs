using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;

public sealed record GetExpiringEmployeesResponseDto(
    List<ExpiringEmployeeDocumentDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record ExpiringEmployeeDocumentDto(
    Guid EmployeeId,
    string FullName,
    Guid ClientEntityId,
    Guid DocumentId,
    DocumentType DocumentType,
    string DocumentNumber,
    DateTime? ExpiryDate,
    int DaysRemaining,
    string Status
);