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
}