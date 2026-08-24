using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Staff.Queries.GetAllStaffWorkload;

public sealed class GetAllStaffWorkloadQueryHandler
    : IRequestHandler<
        GetAllStaffWorkloadQuery,
        GetAllStaffWorkloadResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;

    public GetAllStaffWorkloadQueryHandler(
        IRenewalTaskReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetAllStaffWorkloadResponseDto> Handle(
        GetAllStaffWorkloadQuery request,
        CancellationToken cancellationToken)
    {
        var result =
            await _repository.GetAllStaffWorkloadAsync(
                cancellationToken);

        return result;
    }
}