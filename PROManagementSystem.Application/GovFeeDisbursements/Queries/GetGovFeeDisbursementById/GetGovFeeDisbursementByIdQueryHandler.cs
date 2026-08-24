using MediatR;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;

public sealed class GetGovFeeDisbursementByIdQueryHandler
    : IRequestHandler<
        GetGovFeeDisbursementByIdQuery,
        GetGovFeeDisbursementByIdResponseDto>
{
    private readonly IGovFeeDisbursementReadRepository _readRepository;

    public GetGovFeeDisbursementByIdQueryHandler(
        IGovFeeDisbursementReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<GetGovFeeDisbursementByIdResponseDto> Handle(
        GetGovFeeDisbursementByIdQuery request,
        CancellationToken cancellationToken)
    {
        var result =
            await _readRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

        if (result is null)
        {
            throw new NotFoundException(
                "Gov fee disbursement was not found.");
        }

        return result;
    }
}
