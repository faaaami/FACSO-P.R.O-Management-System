using MediatR;

namespace PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;

public sealed record GetPaymentOrderByIdQuery(
    Guid Id
) : IRequest<GetPaymentOrderByIdResponseDto>;
