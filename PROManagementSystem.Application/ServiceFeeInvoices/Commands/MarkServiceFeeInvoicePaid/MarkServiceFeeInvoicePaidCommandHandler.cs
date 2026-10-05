using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.MarkServiceFeeInvoicePaid;

public sealed class MarkServiceFeeInvoicePaidCommandHandler
    : IRequestHandler<
        MarkServiceFeeInvoicePaidCommand,
        MarkServiceFeeInvoicePaidResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkServiceFeeInvoicePaidCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MarkServiceFeeInvoicePaidResponseDto> Handle(
        MarkServiceFeeInvoicePaidCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Fast existence + status check (not tracked — the FOR UPDATE
            //    read below must see the true DB state).
            var exists = await _context.ServiceFeeInvoices
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.Id && !x.IsDeleted,
                    cancellationToken);

            if (!exists)
            {
                throw new NotFoundException(
                    "Service fee invoice was not found.");
            }

            // 2. Find the related PaymentOrder (if one exists). There is at
            //    most one per invoice because ServiceFeeInvoiceId is unique.
            var relatedOrder =
                await _context.PaymentOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.ServiceFeeInvoiceId == request.Id &&
                            !x.IsDeleted,
                        cancellationToken);

            // 3. Lock the PaymentOrder row first (same order as the webhook
            //    handler) to avoid deadlocks.
            if (relatedOrder is not null)
            {
                var lockedOrders =
                    await _context.PaymentOrders
                        .FromSqlInterpolated(
                            $"SELECT * FROM payment_orders WHERE \"Id\" = {relatedOrder.Id} AND \"IsDeleted\" = false FOR UPDATE")
                        .ToListAsync(cancellationToken);

                relatedOrder = lockedOrders.SingleOrDefault();
            }

            // 4. Lock the invoice row.  This serialises concurrent manual
            //    mark-paid calls for the same invoice (even when there is no
            //    PaymentOrder to lock).  Because no prior read tracked this
            //    entity, the FOR UPDATE result is the fresh DB state.
            var lockedInvoices =
                await _context.ServiceFeeInvoices
                    .FromSqlInterpolated(
                        $"SELECT * FROM service_fee_invoices WHERE \"Id\" = {request.Id} AND \"IsDeleted\" = false FOR UPDATE")
                    .ToListAsync(cancellationToken);

            var invoice = lockedInvoices.SingleOrDefault();

            if (invoice is null)
            {
                throw new NotFoundException(
                    "Service fee invoice was not found.");
            }

            // 5. Re-check after locks.  A concurrent webhook or manual call
            //    may have completed while we waited for the row lock.
            if (invoice.Status !=
                ServiceFeeInvoiceStatus.Pending)
            {
                throw new ConflictException(
                    "Only pending invoices can be marked as paid.");
            }

            var now = DateTime.UtcNow;

            // 6. Mark the invoice Paid.
            invoice.Status =
                ServiceFeeInvoiceStatus.Paid;
            invoice.PaidAt = now;
            invoice.UpdatedAt = now;

            // 7. Mark the related PaymentOrder Paid (if one exists) so the
            //    webhook sees a consistent Paid+Paid state and treats the
            //    duplicate delivery as an idempotent no-op.
            if (relatedOrder is not null)
            {
                EnsureOrderCanBeSettledManually(relatedOrder);

                relatedOrder.Status = PaymentStatus.Paid;

                // PaidAt records when the order was FIRST settled and is
                // immutable from then on.  A pre-existing value means this
                // order was already paid, so keep the original settlement
                // timestamp instead of overwriting it.
                if (relatedOrder.PaidAt is null)
                {
                    relatedOrder.PaidAt = now;
                }

                relatedOrder.UpdatedAt = now;
            }

            // 8. Audit log.
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "ServiceFeeInvoicePaid",
                EntityType = "ServiceFeeInvoice",
                EntityId = invoice.Id,
                Description =
                    $"Service fee invoice '{invoice.InvoiceNumber}' was marked as paid.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            // 9. Single atomic commit.
            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new MarkServiceFeeInvoicePaidResponseDto(
                invoice.Id,
                invoice.ClientCompanyId,
                invoice.RenewalTaskId,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.Amount,
                invoice.Currency,
                invoice.Status,
                invoice.Description,
                invoice.PaidAt,
                invoice.VoidedAt,
                invoice.CreatedAt,
                invoice.UpdatedAt);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Refuses manual settlement of a cancelled payment order.
    /// </summary>
    /// <remarks>
    /// A cancelled order is terminal by definition: both
    /// <c>CancelPaymentOrderCommandHandler</c> and
    /// <c>CreatePaymentOrderCommandHandler</c> treat <c>Cancelled</c> as
    /// final and never resume or repurpose it. Flipping it back to
    /// <c>Paid</c> would therefore resurrect an order whose gateway order has
    /// already been closed, producing a local settlement that no gateway
    /// payment backs and for which there is no <c>RazorpayPaymentId</c> to
    /// evidence it.
    /// <para>
    /// This runs AFTER the order row lock is held, so the status it reads is
    /// the authoritative one and cannot change underneath the decision. A
    /// webhook settling the order first makes this a 409 too, because the
    /// invoice itself is no longer pending.
    /// </para>
    /// </remarks>
    private static void EnsureOrderCanBeSettledManually(
        PaymentOrder relatedOrder)
    {
        if (relatedOrder.Status == PaymentStatus.Cancelled)
        {
            throw new ConflictException(
                "This payment order was cancelled and can no longer be " +
                "marked as paid. Settle the service fee invoice with a new " +
                "payment instead.");
        }
    }
}
