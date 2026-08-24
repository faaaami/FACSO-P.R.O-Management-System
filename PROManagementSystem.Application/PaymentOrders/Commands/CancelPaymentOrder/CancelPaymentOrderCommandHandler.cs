using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CancelPaymentOrder;

public sealed class CancelPaymentOrderCommandHandler
    : IRequestHandler<
        CancelPaymentOrderCommand,
        CancelPaymentOrderResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelPaymentOrderCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CancelPaymentOrderResponseDto> Handle(
        CancelPaymentOrderCommand request,
        CancellationToken cancellationToken)
    {
        var paymentOrder =
            await _context.PaymentOrders
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.Id &&
                        !x.IsDeleted,
                    cancellationToken);

        if (paymentOrder is null)
        {
            throw new NotFoundException(
                "Payment order was not found.");
        }

        if (!_currentUser.IsAdmin &&
            paymentOrder.ClientCompanyId !=
                _currentUser.ClientCompanyId)
        {
            throw new ForbiddenAccessException(
                "You do not have access to this payment order.");
        }

        if (paymentOrder.Status !=
                PaymentStatus.Created &&
            paymentOrder.Status !=
                PaymentStatus.Pending)
        {
            throw new ConflictException(
                $"Cannot cancel a payment order in {paymentOrder.Status} status. Only Created or Pending orders can be cancelled.");
        }

        var now = DateTime.UtcNow;

        paymentOrder.Status =
            PaymentStatus.Cancelled;
        paymentOrder.CancelledAt = now;
        paymentOrder.UpdatedAt = now;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "PaymentOrderCancelled",
            EntityType = "PaymentOrder",
            EntityId = paymentOrder.Id,
            Description =
                $"Payment order '{paymentOrder.RazorpayOrderId}' was cancelled.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new CancelPaymentOrderResponseDto(
            paymentOrder.Id,
            paymentOrder.InvoiceType,
            paymentOrder.RetainerInvoiceId,
            paymentOrder.ServiceFeeInvoiceId,
            paymentOrder.ClientCompanyId,
            paymentOrder.Amount,
            paymentOrder.Currency,
            paymentOrder.Status,
            paymentOrder.CancelledAt,
            paymentOrder.UpdatedAt);
    }
}
