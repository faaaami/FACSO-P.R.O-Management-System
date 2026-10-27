using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Application.PaymentOrders;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.MarkRetainerInvoicePaid;

public sealed class MarkRetainerInvoicePaidCommandHandler
    : IRequestHandler<
        MarkRetainerInvoicePaidCommand,
        MarkRetainerInvoicePaidResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkRetainerInvoicePaidCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MarkRetainerInvoicePaidResponseDto> Handle(
        MarkRetainerInvoicePaidCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Fast existence + status check (not tracked — the FOR UPDATE
            //    read below must see the true DB state).
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

            // 2. Lock the invoice's open payment order, if any (at most one,
            //    enforced by a filtered unique index). Orders are locked
            //    before the invoice, the same order as the webhook handler,
            //    to avoid deadlocks. Closed orders (Cancelled, Failed, Paid,
            //    ...) are history and never block a manual settlement.
            var openOrders =
                await OpenPaymentOrders.LockOpenOrdersAsync(
                    _context,
                    PaymentInvoiceType.RetainerInvoice,
                    request.Id,
                    excludeOrderId: null,
                    cancellationToken);

            // 3. Lock the invoice row.  This serialises concurrent manual
            //    mark-paid calls for the same invoice (even when there is no
            //    PaymentOrder to lock).  Because no prior read tracked this
            //    entity, the FOR UPDATE result is the fresh DB state.
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

            // 4. Re-check after locks.  A concurrent webhook or manual call
            //    may have completed while we waited for the row lock.
            if (invoice.Status !=
                RetainerInvoiceStatus.Pending)
            {
                throw new ConflictException(
                    "Only pending invoices can be marked as paid.");
            }

            var now = DateTime.UtcNow;

            // 5. Mark the invoice Paid.
            invoice.Status =
                RetainerInvoiceStatus.Paid;
            invoice.PaidAt = now;
            invoice.UpdatedAt = now;

            // 6. The money arrived outside the gateway, so an open checkout
            //    must not stay payable: cancel it in the same transaction
            //    (Razorpay cannot close a gateway order). If the client pays
            //    it anyway, the webhook records it as PaidRequiresReview.
            foreach (var openOrder in openOrders)
            {
                OpenPaymentOrders.CancelForSettlement(
                    _context,
                    openOrder,
                    _currentUser.UserId,
                    $"retainer invoice '{invoice.InvoiceNumber}' was marked as paid manually",
                    now);
            }

            // 7. Audit log.
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "RetainerInvoicePaid",
                EntityType = "RetainerInvoice",
                EntityId = invoice.Id,
                Description =
                    $"Retainer invoice '{invoice.InvoiceNumber}' was marked as paid.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            // 8. Single atomic commit.
            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new MarkRetainerInvoicePaidResponseDto(
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
}
