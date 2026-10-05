using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CompletePaymentFromWebhook;

public sealed class CompletePaymentFromWebhookCommandHandler
    : IRequestHandler<
        CompletePaymentFromWebhookCommand,
        CompletePaymentFromWebhookResponseDto>
{
    private const string CapturedEvent = "payment.captured";
    private const string CapturedStatus = "captured";

    private static readonly HashSet<PaymentStatus> CompletableStatuses =
        new()
        {
            PaymentStatus.Created,
            PaymentStatus.Pending
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<CompletePaymentFromWebhookCommandHandler>
        _logger;

    public CompletePaymentFromWebhookCommandHandler(
        IApplicationDbContext context,
        ILogger<CompletePaymentFromWebhookCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CompletePaymentFromWebhookResponseDto> Handle(
        CompletePaymentFromWebhookCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Row-lock the PaymentOrder (serialisation point for concurrent
            // webhooks targeting the same Razorpay order). The locked entity
            // is tracked by EF so later changes persist normally.
            var lockedRows = await _context.PaymentOrders
                .FromSqlInterpolated(
                    $"SELECT * FROM payment_orders WHERE \"RazorpayOrderId\" = {request.RazorpayOrderId} AND \"IsDeleted\" = false FOR UPDATE")
                .ToListAsync(cancellationToken);

            var order = lockedRows.SingleOrDefault();

            // 2. Unknown or soft-deleted order -> no payment applied.
            if (order is null)
            {
                throw new NotFoundException(
                    "Payment order was not found.");
            }

            // 3. Idempotent duplicate: already paid -> clean no-op.
            if (order.Status == PaymentStatus.Paid)
            {
                // A Paid order that carries no gateway payment id is an
                // ANOMALY, not a normal duplicate: the money was recorded
                // without any evidence from the provider. Manual settlement
                // can no longer produce this state (a cancelled order is
                // refused outright), so an occurrence means drifted or
                // manually edited data and the evidence for this delivery is
                // being dropped silently. Surface it instead of swallowing it.
                //
                // The delivery is still treated as a no-op on purpose: a
                // genuine duplicate must stay idempotent, and re-applying it
                // would be worse than logging it.
                if (order.RazorpayPaymentId is null)
                {
                    _logger.LogWarning(
                        "Webhook '{EventId}' for an already-Paid order " +
                        "{OrderId} (RazorpayOrderId {RazorpayOrderId}) left " +
                        "no payment id on the order. The delivery was treated " +
                        "as a duplicate and discarded, so this capture has no " +
                        "local record. Investigate: paymentId={PaymentId}.",
                        request.WebhookEventId,
                        order.Id,
                        request.RazorpayOrderId,
                        request.RazorpayPaymentId);
                }

                await transaction.RollbackAsync(cancellationToken);
                return new CompletePaymentFromWebhookResponseDto(
                    order.Id,
                    order.RazorpayOrderId!,
                    order.Status,
                    order.PaidAt,
                    request.WebhookEventId,
                    Completed: false);
            }

            // 4. Only a captured payment completes.
            if (!string.Equals(
                    request.PaymentEventName,
                    CapturedEvent,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    request.PaymentStatus,
                    CapturedStatus,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "Payment is not captured.");
            }

            // 5. PaymentOrder must be in a completable state.
            if (!CompletableStatuses.Contains(order.Status))
            {
                throw new ConflictException(
                    $"Payment order is {order.Status}. " +
                    "Only pending orders can be paid.");
            }

            // 6. Amount + currency must match the persisted order.
            if (request.AmountInSmallestUnit !=
                (int)(order.Amount * 100))
            {
                throw new ConflictException(
                    "Payment amount does not match the payment order.");
            }

            if (!string.Equals(
                    request.Currency,
                    order.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "Payment currency does not match the payment order.");
            }

            // 7. Derive the invoice from the persisted PaymentOrder only
            // (never from request data). Validate the order<->invoice
            // relationship and payability.
            var now = DateTime.UtcNow;

            if (order.InvoiceType ==
                PaymentInvoiceType.RetainerInvoice)
            {
                if (!order.RetainerInvoiceId.HasValue)
                {
                    throw new ConflictException(
                        "Retainer invoice is missing on the payment order.");
                }

                var invoice = await _context.RetainerInvoices
                    .FirstOrDefaultAsync(
                        x => x.Id == order.RetainerInvoiceId.Value &&
                             !x.IsDeleted,
                        cancellationToken);

                if (invoice is null)
                {
                    throw new NotFoundException(
                        "Retainer invoice was not found.");
                }

                if (invoice.Status != RetainerInvoiceStatus.Pending)
                {
                    throw new ConflictException(
                        $"Invoice is {invoice.Status}. " +
                        "Only pending invoices can be paid.");
                }

                order.Status = PaymentStatus.Paid;
                order.RazorpayPaymentId = request.RazorpayPaymentId;
                order.RazorpaySignature = request.RazorpaySignature;
                order.PaidAt = now;
                order.UpdatedAt = now;

                invoice.Status = RetainerInvoiceStatus.Paid;
                invoice.PaidAt = now;
                invoice.UpdatedAt = now;
            }
            else if (order.InvoiceType ==
                PaymentInvoiceType.ServiceFeeInvoice)
            {
                if (!order.ServiceFeeInvoiceId.HasValue)
                {
                    throw new ConflictException(
                        "Service fee invoice is missing on the payment order.");
                }

                var invoice = await _context.ServiceFeeInvoices
                    .FirstOrDefaultAsync(
                        x => x.Id == order.ServiceFeeInvoiceId.Value &&
                             !x.IsDeleted,
                        cancellationToken);

                if (invoice is null)
                {
                    throw new NotFoundException(
                        "Service fee invoice was not found.");
                }

                if (invoice.Status != ServiceFeeInvoiceStatus.Pending)
                {
                    throw new ConflictException(
                        $"Invoice is {invoice.Status}. " +
                        "Only pending invoices can be paid.");
                }

                order.Status = PaymentStatus.Paid;
                order.RazorpayPaymentId = request.RazorpayPaymentId;
                order.RazorpaySignature = request.RazorpaySignature;
                order.PaidAt = now;
                order.UpdatedAt = now;

                invoice.Status = ServiceFeeInvoiceStatus.Paid;
                invoice.PaidAt = now;
                invoice.UpdatedAt = now;
            }
            else
            {
                throw new ConflictException(
                    "Unsupported payment order invoice type.");
            }

            // 8. Exactly one audit log for the successful completion.
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = null,
                Action = "PaymentCompletedFromWebhook",
                EntityType = "PaymentOrder",
                EntityId = order.Id,
                Description =
                    $"Payment order '{order.RazorpayOrderId}' was marked " +
                    "as paid via Razorpay webhook.",
                Metadata = $"eventId={request.WebhookEventId}",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            // 9. Single atomically-committed save (order + invoice + audit).
            await _context.SaveChangesAsync(cancellationToken);

            // 10. Commit.
            await transaction.CommitAsync(cancellationToken);

            return new CompletePaymentFromWebhookResponseDto(
                order.Id,
                order.RazorpayOrderId!,
                order.Status,
                order.PaidAt,
                request.WebhookEventId,
                Completed: true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

