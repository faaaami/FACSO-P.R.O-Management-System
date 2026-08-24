using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;

public sealed record GetPaymentOrdersQuery(
    Guid? ClientCompanyId,
    PaymentStatus? Status,
    int Page,
    int PageSize
) : IRequest<GetPaymentOrdersResponseDto>;
