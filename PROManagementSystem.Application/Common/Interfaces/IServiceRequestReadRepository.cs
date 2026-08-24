using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

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
}