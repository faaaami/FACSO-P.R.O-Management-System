using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders;

/// <summary>
/// Shared rules for the "at most one open payment order per invoice" model.
/// An order is open while it is Created or Pending; every other status is
/// closed and never reopened.
/// </summary>
internal static class OpenPaymentOrders
{
    public static bool IsOpen(PaymentStatus status) =>
        status is PaymentStatus.Created or PaymentStatus.Pending;

    /// <summary>
    /// Row-locks (FOR UPDATE) the open orders of an invoice, optionally
    /// excluding one order the caller already holds. Callers lock orders
    /// BEFORE the invoice row, the same order every payment handler uses.
    /// </summary>
    public static async Task<List<PaymentOrder>> LockOpenOrdersAsync(
        IApplicationDbContext context,
        PaymentInvoiceType invoiceType,
        Guid invoiceId,
        Guid? excludeOrderId,
        CancellationToken cancellationToken)
    {
        var query = invoiceType == PaymentInvoiceType.RetainerInvoice
            ? context.PaymentOrders.FromSqlInterpolated(
                $"SELECT * FROM payment_orders WHERE \"RetainerInvoiceId\" = {invoiceId} AND \"IsDeleted\" = false AND \"Status\" IN (1, 2) ORDER BY \"Id\" FOR UPDATE")
            : context.PaymentOrders.FromSqlInterpolated(
                $"SELECT * FROM payment_orders WHERE \"ServiceFeeInvoiceId\" = {invoiceId} AND \"IsDeleted\" = false AND \"Status\" IN (1, 2) ORDER BY \"Id\" FOR UPDATE");

        var orders = await query.ToListAsync(cancellationToken);

        return excludeOrderId.HasValue
            ? orders.Where(x => x.Id != excludeOrderId.Value).ToList()
            : orders;
    }

    /// <summary>
    /// Cancels a locked open order because its invoice was settled another
    /// way. Only the local order is retired: Razorpay cannot close an order,
    /// so a later capture on it is caught by the webhook as
    /// PaidRequiresReview instead of being lost.
    /// </summary>
    public static void CancelForSettlement(
        IApplicationDbContext context,
        PaymentOrder order,
        Guid? userId,
        string reason,
        DateTime now)
    {
        order.Status = PaymentStatus.Cancelled;
        order.CancelledAt = now;
        order.UpdatedAt = now;

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "PaymentOrderCancelled",
            EntityType = "PaymentOrder",
            EntityId = order.Id,
            Description =
                $"Payment order '{order.RazorpayOrderId}' was cancelled: {reason}.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        });
    }
}
