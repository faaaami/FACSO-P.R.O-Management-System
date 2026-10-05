using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IServiceRequestReadRepository
{
    Task<GetServiceRequestsResponseDto> GetServiceRequestsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetServiceRequestResponseDto?> GetServiceRequestByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<GetServiceRequestsResponseDto> GetServiceRequestsByCompanyAsync(
        Guid clientCompanyId,
        ServiceRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetServiceRequestResponseDto?> GetServiceRequestByIdForCompanyAsync(
        Guid id,
        Guid clientCompanyId,
        CancellationToken cancellationToken);
}