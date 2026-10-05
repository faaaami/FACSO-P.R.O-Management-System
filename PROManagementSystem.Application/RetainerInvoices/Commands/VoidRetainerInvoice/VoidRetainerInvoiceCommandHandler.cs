using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
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
    private readonly IRazorpayService _razorpayService;

    public VoidRetainerInvoiceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRazorpayService razorpayService)
    {
        _context = context;
        _currentUser = currentUser;
        _razorpayService = razorpayService;
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

            // 2. Find the related PaymentOrder (if one exists). There is at
            //    most one per invoice because RetainerInvoiceId is unique.
            var relatedOrder =
                await _context.PaymentOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.RetainerInvoiceId == request.Id &&
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

            // 6. Retire the related PaymentOrder. A live gateway order over a
            //    voided invoice is the dangerous state: the client can still
            //    complete checkout against it, and the webhook would then
            //    reject the payment because the invoice is Void - the money is
            //    taken and never recorded. Close the gateway first and roll
            //    the whole void back if that fails, so a failed close leaves
            //    both the order and the invoice untouched and retryable.
            if (relatedOrder is not null &&
                ClosableStatuses.Contains(relatedOrder.Status))
            {
                if (relatedOrder.RazorpayOrderId is not null)
                {
                    await _razorpayService
                        .CloseOrderAsync(
                            relatedOrder.RazorpayOrderId,
                            cancellationToken);
                }

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

            // 8. Single atomic commit: the order is retired and the invoice
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
