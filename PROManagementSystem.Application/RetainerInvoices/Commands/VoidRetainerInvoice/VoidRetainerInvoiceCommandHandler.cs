using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Notifications;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.VoidRetainerInvoice;

public sealed class VoidRetainerInvoiceCommandHandler
    : IRequestHandler<
        VoidRetainerInvoiceCommand,
        VoidRetainerInvoiceResponseDto>
{
    /// <summary>
    /// Order statuses a void is allowed to retire. Paid and Cancelled orders
    /// are already terminal, so there is nothing to close.
    /// </summary>
    private static readonly HashSet<PaymentStatus> ClosableStatuses =
        new()
        {
            PaymentStatus.Created,
            PaymentStatus.Pending
        };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;

    public VoidRetainerInvoiceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IUserNotifier notifier)
    {
        _context = context;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<VoidRetainerInvoiceResponseDto> Handle(
        VoidRetainerInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Fast existence check (not tracked - the FOR UPDATE read below
            //    must see the true DB state).
            var exists = await _context.RetainerInvoices
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.Id && !x.IsDeleted,
                    cancellationToken);

            if (!exists)
            {
                throw new NotFoundException(
                    "Retainer invoice was not found.");
            }

            // 2. Find the invoice's open PaymentOrder (if one exists). An
            //    invoice can have many closed orders but at most one open one,
            //    enforced by a filtered unique index on RetainerInvoiceId.
            var relatedOrder =
                await _context.PaymentOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.RetainerInvoiceId == request.Id &&
                            (x.Status == PaymentStatus.Created ||
                             x.Status == PaymentStatus.Pending) &&
                            !x.IsDeleted,
                        cancellationToken);

            // 3. Lock the PaymentOrder row first (same order as the webhook
            //    and mark-paid handlers) to avoid deadlocks.
            if (relatedOrder is not null)
            {
                var lockedOrders =
                    await _context.PaymentOrders
                        .FromSqlInterpolated(
                            $"SELECT * FROM payment_orders WHERE \"Id\" = {relatedOrder.Id} AND \"IsDeleted\" = false FOR UPDATE")
                        .ToListAsync(cancellationToken);

                relatedOrder = lockedOrders.SingleOrDefault();
            }

            // 4. Lock the invoice row. This serialises concurrent void calls
            //    and races them against the webhook and mark-paid handlers.
            var lockedInvoices =
                await _context.RetainerInvoices
                    .FromSqlInterpolated(
                        $"SELECT * FROM retainer_invoices WHERE \"Id\" = {request.Id} AND \"IsDeleted\" = false FOR UPDATE")
                    .ToListAsync(cancellationToken);

            var invoice = lockedInvoices.SingleOrDefault();

            if (invoice is null)
            {
                throw new NotFoundException(
                    "Retainer invoice was not found.");
            }

            // 5. MANDATORY post-lock re-check. A webhook may have settled the
            //    invoice while we waited for the row lock. Voids must never
            //    overwrite a settlement, because that would hide captured
            //    money behind a voided invoice.
            if (invoice.Status !=
                RetainerInvoiceStatus.Pending)
            {
                throw new ConflictException(
                    "Only pending invoices can be voided.");
            }

            var now = DateTime.UtcNow;

            // 6. Retire the related PaymentOrder locally. Razorpay cannot
            //    close a gateway order, so if the client still completes
            //    checkout on it, the webhook records the money as
            //    PaidRequiresReview against the voided invoice and notifies
            //    admins - it is never lost.
            if (relatedOrder is not null &&
                ClosableStatuses.Contains(relatedOrder.Status))
            {
                relatedOrder.Status = PaymentStatus.Cancelled;
                relatedOrder.CancelledAt = now;
                relatedOrder.UpdatedAt = now;

                _context.AuditLogs.Add(new Domain.Entities.AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUser.UserId,
                    Action = "PaymentOrderCancelled",
                    EntityType = "PaymentOrder",
                    EntityId = relatedOrder.Id,
                    Description =
                        $"Payment order '{relatedOrder.RazorpayOrderId}' " +
                        $"was cancelled because retainer invoice " +
                        $"'{invoice.InvoiceNumber}' was voided.",
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            // 7. Void the invoice.
            invoice.Status = RetainerInvoiceStatus.Void;
            invoice.VoidedAt = now;
            invoice.VoidReason = request.Reason?.Trim();
            invoice.UpdatedAt = now;

            var auditLog = new Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "RetainerInvoiceVoided",
                EntityType = "RetainerInvoice",
                EntityId = invoice.Id,
                Description =
                    $"Retainer invoice '{invoice.InvoiceNumber}' was voided.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };

            _context.AuditLogs.Add(auditLog);

            // 8. Tell the client the invoice no longer needs paying. Saved
            //    with the void below and pushed only after the commit.
            await _notifier.NotifyClientContactsAsync(
                invoice.ClientCompanyId,
                "Invoice cancelled",
                $"Invoice {invoice.InvoiceNumber} " +
                $"({NotificationLabels.Money(invoice.Amount, invoice.Currency)}) " +
                "was cancelled and no longer needs to be paid.",
                cancellationToken);

            // 9. Single atomic commit: the order is retired and the invoice
            //    is voided together or not at all.
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new VoidRetainerInvoiceResponseDto(
                invoice.Id,
                invoice.ClientCompanyId,
                invoice.ServiceContractId,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                invoice.Amount,
                invoice.Currency,
                invoice.Status,
                invoice.Notes,
                invoice.PaidAt,
                invoice.VoidedAt,
                invoice.VoidReason,
                invoice.CreatedAt,
                invoice.UpdatedAt);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
