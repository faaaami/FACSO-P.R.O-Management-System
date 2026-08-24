using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;

public sealed class GetStaffWorkloadQueryHandler
    : IRequestHandler<
        GetStaffWorkloadQuery,
        GetStaffWorkloadResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;

    public GetStaffWorkloadQueryHandler(
        IRenewalTaskReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetStaffWorkloadResponseDto?> Handle(
        GetStaffWorkloadQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetStaffWorkloadAsync(
            request.StaffId,
            cancellationToken);
    }
}