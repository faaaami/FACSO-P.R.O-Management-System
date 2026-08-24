using MediatR;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CancelPaymentOrder;

public sealed record CancelPaymentOrderCommand(
    Guid Id
) : IRequest<CancelPaymentOrderResponseDto>;
