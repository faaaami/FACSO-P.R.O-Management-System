using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;
using PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Application.Staff.Queries.GetAllStaffWorkload;
using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;
using PROManagementSystem.Application.Tasks.Queries.GetAgentServiceFeeInvoice;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IRenewalTaskReadRepository
{
    Task<GetRenewalTasksResponseDto> GetTasksAsync(
        RenewalTaskStatus? status,
        Guid? assignedStaffId,
        Guid? clientCompanyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetRenewalTaskByIdResponseDto?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    Task<GetRenewalTaskStepsResponseDto?> GetStepsAsync(
    Guid taskId,
    CancellationToken cancellationToken);

    Task<GetRenewalTaskHistoryResponseDto?> GetHistoryAsync(
    Guid taskId,
    CancellationToken cancellationToken);

    Task<GetRenewalTaskByIdResponseDto?> GetByIdForCompanyAsync(
        Guid taskId,
        Guid clientCompanyId,
        CancellationToken cancellationToken);

    Task<GetRenewalTaskHistoryResponseDto?> GetHistoryForCompanyAsync(
        Guid taskId,
        Guid clientCompanyId,
        CancellationToken cancellationToken);

    Task<GetStaffWorkloadResponseDto?> GetStaffWorkloadAsync(
    Guid staffId,
    CancellationToken cancellationToken);

    Task<GetAllStaffWorkloadResponseDto> GetAllStaffWorkloadAsync(
    CancellationToken cancellationToken);

    Task<GetRenewalTaskByIdResponseDto?> GetAgentTaskByIdAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken);

    Task<GetRenewalTaskStepsResponseDto?> GetAgentStepsAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken);

    Task<GetRenewalTaskHistoryResponseDto?> GetAgentHistoryAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken);

    Task<GetAgentServiceFeeInvoiceResponseDto?> GetServiceFeeInvoiceByTaskIdAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken);

    // ============================================================
    // Agent client/entity/employee/document scope
    // ============================================================

    Task<GetClientCompaniesResponseDto> GetAgentClientCompaniesAsync(
        Guid agentId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task<GetClientCompanyByIdResponseDto?> GetAgentClientCompanyByIdAsync(
        Guid agentId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<GetClientEntitiesResponseDto> GetAgentClientEntitiesAsync(
        Guid agentId,
        Guid companyId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task<GetEmployeesResponseDto> GetAgentEmployeesAsync(
        Guid agentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetEmployeeByIdResponseDto?> GetAgentEmployeeByIdAsync(
        Guid agentId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<GetEmployeeDocumentsResponseDto> GetAgentEmployeeDocumentsAsync(
        Guid agentId,
        Guid employeeId,
        CancellationToken cancellationToken);

    // ============================================================
    // Agent document scope checks
    // ============================================================

    Task<bool> IsAgentDocumentInScopeAsync(
        Guid agentId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<bool> IsAgentEmployeeInScopeAsync(
        Guid agentId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<bool> IsAgentEntityInScopeAsync(
        Guid agentId,
        Guid entityId,
        CancellationToken cancellationToken);

    Task<bool> IsAgentClientInScopeAsync(
        Guid agentId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<GetDocumentDependenciesResponseDto> GetAgentDocumentDependenciesAsync(
        Guid agentId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<GetAgentDocumentsResponseDto> GetAgentDocumentsAsync(
        Guid agentId,
        DocumentType? type,
        DocumentStatus? status,
        DateTime? expiresBefore,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetExpiringDocumentsResponseDto> GetAgentExpiringDocumentsAsync(
        Guid agentId,
        int days,
        int page,
        int pageSize,
        bool includeExpired,
        CancellationToken cancellationToken);
}