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
    /// <summary>
    /// Statuses a payment order may still be cancelled from. These are the
    /// same resumable states <c>CreatePaymentOrderCommandHandler</c> treats
    /// as reusable, so cancelling removes the order from the set the checkout
    /// will hand back. Paid and Cancelled are terminal.
    /// </summary>
    private static readonly HashSet<PaymentStatus> CancellableStatuses =
        new()
        {
            PaymentStatus.Created,
            PaymentStatus.Pending
        };

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
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Fast existence check (not tracked - the FOR UPDATE read
            //    below must see the true DB state).
            var exists = await _context.PaymentOrders
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.Id && !x.IsDeleted,
                    cancellationToken);

            if (!exists)
            {
                throw new NotFoundException(
                    "Payment order was not found.");
            }

            // 2. Lock the order row. This is the same row the webhook locks,
            //    so a webhook settling the payment and this cancellation are
            //    serialised against each other instead of racing.
            var lockedOrders =
                await _context.PaymentOrders
                    .FromSqlInterpolated(
                        $"SELECT * FROM payment_orders WHERE \"Id\" = {request.Id} AND \"IsDeleted\" = false FOR UPDATE")
                    .ToListAsync(cancellationToken);

            var paymentOrder = lockedOrders.SingleOrDefault();

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

            // 3. MANDATORY post-lock re-check. A webhook or a competing
            //    cancellation may have completed while we waited for the lock.
            if (!CancellableStatuses.Contains(paymentOrder.Status))
            {
                throw new ConflictException(
                    $"Cannot cancel a payment order in {paymentOrder.Status} status. Only Created or Pending orders can be cancelled.");
            }

            // 4. The invoice must still be payable. Without this, an order
            //    could be cancelled out from under an invoice that is already
            //    settled, leaving a Paid invoice with no live order.
            await EnsureInvoiceStillPayableAsync(
                paymentOrder,
                cancellationToken);

            // 5. Cancel locally. Razorpay cannot close a gateway order, so a
            //    payment the client still completes on it reaches the
            //    webhook, which settles the invoice if it is still pending or
            //    records the money as PaidRequiresReview - it is never lost.
            var now = DateTime.UtcNow;

            paymentOrder.Status = PaymentStatus.Cancelled;
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

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

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
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Refuses to cancel when the order's invoice is no longer pending, so a
    /// cancellation can never strand a settled or voided invoice.
    /// </summary>
    private async Task EnsureInvoiceStillPayableAsync(
        Domain.Entities.PaymentOrder paymentOrder,
        CancellationToken cancellationToken)
    {
        if (paymentOrder.InvoiceType ==
                PaymentInvoiceType.RetainerInvoice &&
            paymentOrder.RetainerInvoiceId.HasValue)
        {
            var retainer = await _context.RetainerInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == paymentOrder.RetainerInvoiceId.Value &&
                        !x.IsDeleted,
                    cancellationToken);

            if (retainer is not null &&
                retainer.Status != RetainerInvoiceStatus.Pending)
            {
                throw new ConflictException(
                    $"The linked retainer invoice is {retainer.Status} and can no longer be cancelled through its payment order.");
            }

            return;
        }

        if (paymentOrder.InvoiceType ==
                PaymentInvoiceType.ServiceFeeInvoice &&
            paymentOrder.ServiceFeeInvoiceId.HasValue)
        {
            var serviceFee = await _context.ServiceFeeInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == paymentOrder.ServiceFeeInvoiceId.Value &&
                        !x.IsDeleted,
                    cancellationToken);

            if (serviceFee is not null &&
                serviceFee.Status !=
                    ServiceFeeInvoiceStatus.Pending)
            {
                throw new ConflictException(
                    $"The linked service fee invoice is {serviceFee.Status} and can no longer be cancelled through its payment order.");
            }
        }
    }
}
