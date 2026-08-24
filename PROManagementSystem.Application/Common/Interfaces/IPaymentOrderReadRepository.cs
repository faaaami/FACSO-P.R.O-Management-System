using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IPaymentOrderReadRepository
{
    Task<GetPaymentOrdersResponseDto?> GetListAsync(
        Guid? clientCompanyId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetPaymentOrderByIdResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
