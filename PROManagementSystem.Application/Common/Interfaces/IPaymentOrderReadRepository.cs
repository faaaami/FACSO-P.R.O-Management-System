using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IPaymentOrderReadRepository
{
    Task<GetPaymentOrdersResponseDto?> GetListAsync(
        Guid? clientCompanyId,
        PaymentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetPaymentOrderByIdResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
