using MediatR;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;

public sealed class GetPaymentOrderByIdQueryHandler
    : IRequestHandler<
        GetPaymentOrderByIdQuery,
        GetPaymentOrderByIdResponseDto>
{
    private readonly IPaymentOrderReadRepository _readRepository;

    public GetPaymentOrderByIdQueryHandler(
        IPaymentOrderReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<GetPaymentOrderByIdResponseDto> Handle(
        GetPaymentOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var result =
            await _readRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

        if (result is null)
        {
            throw new NotFoundException(
                "Payment order was not found.");
        }

        return result;
    }
}
