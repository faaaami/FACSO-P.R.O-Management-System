using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;

public sealed class GetPaymentOrdersQueryHandler
    : IRequestHandler<
        GetPaymentOrdersQuery,
        GetPaymentOrdersResponseDto>
{
    private readonly IPaymentOrderReadRepository _readRepository;

    public GetPaymentOrdersQueryHandler(
        IPaymentOrderReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<GetPaymentOrdersResponseDto> Handle(
        GetPaymentOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var result =
            await _readRepository.GetListAsync(
                request.ClientCompanyId,
                request.Status,
                request.Page,
                request.PageSize,
                cancellationToken);

        return result!;
    }
}
