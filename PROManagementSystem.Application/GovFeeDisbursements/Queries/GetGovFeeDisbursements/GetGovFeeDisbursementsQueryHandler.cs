using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;

public sealed class GetGovFeeDisbursementsQueryHandler
    : IRequestHandler<
        GetGovFeeDisbursementsQuery,
        GetGovFeeDisbursementsResponseDto>
{
    private readonly IGovFeeDisbursementReadRepository _readRepository;

    public GetGovFeeDisbursementsQueryHandler(
        IGovFeeDisbursementReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<GetGovFeeDisbursementsResponseDto> Handle(
        GetGovFeeDisbursementsQuery request,
        CancellationToken cancellationToken)
    {
        var result =
            await _readRepository.GetListAsync(
                request.ClientCompanyId,
                request.Status?.ToString(),
                request.Page,
                request.PageSize,
                cancellationToken);

        return result!;
    }
}
