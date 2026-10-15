using System.Net;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Dashboard;

/// <summary>
/// P10.2 REGRESSION SUITE - Admin dashboard summary pending-invoice counts.
///
/// THE DEFECT
///
/// <c>DashboardReadRepository.GetSummaryAsync</c> counted pending invoices with
///
///   retainer_invoices   WHERE "IsDeleted" = false AND "Status" = 0
///   service_fee_invoices WHERE "IsDeleted" = false AND "Status" = 0
///
/// but the domain enums are
///
///   RetainerInvoiceStatus   Pending = 1, Paid = 2, Void = 3
///   ServiceFeeInvoiceStatus Pending = 1, Paid = 2, Void = 3
///
/// Zero is not a member of either enum and no invoice can carry it, so both
/// counters were structurally incapable of returning anything but 0. They were
/// not merely imprecise: they were unreachable by construction.
///
/// WHAT THIS IS NOT
///
/// This is invoice <c>Status</c>, NOT <c>PaymentStatus</c>. PaymentStatus lives
/// on payment_orders and has its own independent lifecycle (Created / Pending /
/// Paid / Failed / Cancelled / Refunded). T7-T9 exist to pin that the summary
/// reads the invoice alone: a payment order in ANY state, including Paid, must
/// not move the invoice's pending count in either direction.
///
/// ISOLATION DISCIPLINE
///
/// Every test clears the seeded invoices and plants exactly the rows it
/// describes, so the expected numbers are absolute rather than relative to
/// whatever the shared seed happens to contain. Payment orders are cleared for
/// the same reason, and the table starts empty.
///
/// The four non-payment KPIs are guarded by T11 comparing two reads rather than
/// by hard-coded seed totals, so this suite cannot break when unrelated seed
/// data changes.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DashboardSummaryInvoiceCountsTests : IntegrationTestBase
{
    public DashboardSummaryInvoiceCountsTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;

    // ==================================================================
    // Read helpers
    // ==================================================================

    private async Task<DashboardSummaryDto> ReadSummaryAsync()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/dashboard/summary", Admin, "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<DashboardSummaryDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<DashboardSummaryDto>(envelope!.Data);
    }

    private async Task<DashboardRevenueDto> ReadRevenueAsync(
        string period = "month")
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/dashboard/revenue?period={period}",
                Admin, "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<DashboardRevenueDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<DashboardRevenueDto>(envelope!.Data);
    }

    // ==================================================================
    // Seeding helpers
    // ==================================================================

    /// <summary>
    /// Empties the invoice and payment-order tables so a test's expected counts
    /// are absolute. Payment orders are cleared first because they reference
    /// invoices.
    /// </summary>
    private async Task ResetBillingTablesAsync()
    {
        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.RemoveRange(db.PaymentOrders.ToList());
            db.RetainerInvoices.RemoveRange(
                db.RetainerInvoices.ToList());
            db.ServiceFeeInvoices.RemoveRange(
                db.ServiceFeeInvoices.ToList());

            db.SaveChanges();
        });
    }

    private static RetainerInvoice NewRetainerInvoice(
        RetainerInvoiceStatus status,
        bool isDeleted = false,
        DateTime? paidAt = null)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        return new RetainerInvoice
        {
            Id = id,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            ServiceContractId = TestDataSeeder.Ids.ContractA,
            InvoiceNumber = $"P102-R-{id:N}"[..24],
            InvoiceDate = DateOnly.FromDateTime(now.AddDays(-20)),
            DueDate = DateOnly.FromDateTime(now.AddDays(10)),
            PeriodStart = DateOnly.FromDateTime(now.AddDays(-30)),
            PeriodEnd = DateOnly.FromDateTime(now.AddDays(1)),
            Amount = 2500m,
            Currency = "AED",
            Status = status,
            Notes = "P10.2 probe.",
            PaidAt = paidAt
                ?? (status == RetainerInvoiceStatus.Paid
                    ? now.AddDays(-5)
                    : null),
            VoidedAt = status == RetainerInvoiceStatus.Void
                ? now.AddDays(-2)
                : null,
            VoidReason = status == RetainerInvoiceStatus.Void
                ? "P10.2 probe"
                : null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? now : null
        };
    }

    private static ServiceFeeInvoice NewServiceFeeInvoice(
        ServiceFeeInvoiceStatus status,
        bool isDeleted = false,
        DateTime? paidAt = null)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        return new ServiceFeeInvoice
        {
            Id = id,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            // RenewalTaskId carries a UNIQUE index, so every service-fee
            // invoice probe needs its own task. Assigned by the caller through
            // AddServiceFeeInvoice.
            RenewalTaskId = Guid.Empty,
            InvoiceNumber = $"P102-SF-{id:N}"[..24],
            InvoiceDate = DateOnly.FromDateTime(now.AddDays(-10)),
            DueDate = DateOnly.FromDateTime(now.AddDays(5)),
            Amount = 150m,
            Currency = "AED",
            Status = status,
            Description = "P10.2 probe.",
            PaidAt = paidAt
                ?? (status == ServiceFeeInvoiceStatus.Paid
                    ? now.AddDays(-5)
                    : null),
            VoidedAt = status == ServiceFeeInvoiceStatus.Void
                ? now.AddDays(-2)
                : null,
            VoidReason = status == ServiceFeeInvoiceStatus.Void
                ? "P10.2 probe"
                : null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? now : null
        };
    }

    private static void AddRetainerInvoice(
        IApplicationDbContext db,
        RetainerInvoiceStatus status,
        bool isDeleted = false,
        DateTime? paidAt = null)
        => db.RetainerInvoices.Add(
            NewRetainerInvoice(status, isDeleted, paidAt));

    private static ServiceFeeInvoice AddServiceFeeInvoice(
        IApplicationDbContext db,
        ServiceFeeInvoiceStatus status,
        bool isDeleted = false,
        DateTime? paidAt = null)
    {
        var now = DateTime.UtcNow;
        var documentId = Guid.NewGuid();

        // renewal_tasks carries a UNIQUE index on DocumentId (the P9.2
        // live-task slot), and service_fee_invoices carries a UNIQUE index on
        // RenewalTaskId. Each probe therefore needs its own document AND its own
        // task rather than reusing a seeded pair.
        db.Documents.Add(new Document
        {
            Id = documentId,
            ClientEntityId = TestDataSeeder.Ids.EntityA,
            EmployeeId = null,
            Type = DocumentType.Visa,
            DocumentNumber = $"P102-{documentId:N}"[..20],
            IssueDate = now.AddYears(-1),
            ExpiryDate = now.AddDays(200),
            FileUrl = $"https://storage.test/{documentId:N}",
            FileName = "p102-probe.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        });

        var task = new RenewalTask
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            AssignedStaffId = null,
            Status = RenewalTaskStatus.Submitted,
            ServiceFeeAmount = 150m,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        db.RenewalTasks.Add(task);

        var invoice = NewServiceFeeInvoice(status, isDeleted, paidAt);
        invoice.RenewalTaskId = task.Id;

        db.ServiceFeeInvoices.Add(invoice);

        return invoice;
    }

    private static void AddPaymentOrderForRetainer(
        IApplicationDbContext db,
        RetainerInvoice invoice,
        PaymentStatus status)
    {
        var now = DateTime.UtcNow;

        db.PaymentOrders.Add(new PaymentOrder
        {
            Id = Guid.NewGuid(),
            InvoiceType = PaymentInvoiceType.RetainerInvoice,
            RetainerInvoiceId = invoice.Id,
            ServiceFeeInvoiceId = null,
            ClientCompanyId = invoice.ClientCompanyId,
            Amount = invoice.Amount,
            Currency = invoice.Currency,
            Status = status,
            RazorpayOrderId = $"order_{Guid.NewGuid():N}"[..32],
            RazorpayPaymentId = status == PaymentStatus.Paid
                ? $"pay_{Guid.NewGuid():N}"[..32]
                : null,
            RazorpaySignature = null,
            PaidAt = status == PaymentStatus.Paid ? now.AddDays(-1) : null,
            FailedAt = status == PaymentStatus.Failed ? now.AddDays(-1) : null,
            CancelledAt =
                status == PaymentStatus.Cancelled ? now.AddDays(-1) : null,
            Notes = "P10.2 probe.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        });
    }

    // ==================================================================
    // T1 / T2 - the discriminator
    // ==================================================================

    [Fact(DisplayName =
        "T1: one Pending + one Paid + one Void retainer invoice yields " +
        "PendingRetainerInvoices == 1")]
    public async Task RetainerInvoiceSummary_CountsOnlyPending()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddRetainerInvoice(db, RetainerInvoiceStatus.Pending);
            AddRetainerInvoice(db, RetainerInvoiceStatus.Paid);
            AddRetainerInvoice(db, RetainerInvoiceStatus.Void);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(1, summary.PendingRetainerInvoices);
    }

    [Fact(DisplayName =
        "T2: one Pending + one Paid + one Void service-fee invoice yields " +
        "PendingServiceFeeInvoices == 1")]
    public async Task ServiceFeeInvoiceSummary_CountsOnlyPending()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Pending);
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Paid);
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Void);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(1, summary.PendingServiceFeeInvoices);
    }

    // ==================================================================
    // T3 / T4 - Paid and Void are excluded
    // ==================================================================

    [Fact(DisplayName =
        "T3: a Paid invoice is never counted as pending")]
    public async Task APaidInvoice_IsNeverCountedAsPending()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddRetainerInvoice(db, RetainerInvoiceStatus.Paid);
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Paid);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(0, summary.PendingRetainerInvoices);
        Assert.Equal(0, summary.PendingServiceFeeInvoices);
    }

    [Fact(DisplayName =
        "T4: a Void invoice is never counted as pending")]
    public async Task AVoidInvoice_IsNeverCountedAsPending()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddRetainerInvoice(db, RetainerInvoiceStatus.Void);
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Void);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(0, summary.PendingRetainerInvoices);
        Assert.Equal(0, summary.PendingServiceFeeInvoices);
    }

    // ==================================================================
    // T5 / T6 - soft delete, and a genuine zero
    // ==================================================================

    [Fact(DisplayName =
        "T5: a soft-deleted Pending invoice is excluded from the count")]
    public async Task ASoftDeletedPendingInvoice_IsExcluded()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddRetainerInvoice(
                db, RetainerInvoiceStatus.Pending, isDeleted: true);
            AddServiceFeeInvoice(
                db, ServiceFeeInvoiceStatus.Pending, isDeleted: true);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(0, summary.PendingRetainerInvoices);
        Assert.Equal(0, summary.PendingServiceFeeInvoices);
    }

    [Fact(DisplayName =
        "T6: with no pending invoices the counts are exactly 0")]
    public async Task NoPendingInvoices_ReportsZeroNotNull()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddRetainerInvoice(db, RetainerInvoiceStatus.Paid);
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Void);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(0, summary.PendingRetainerInvoices);
        Assert.Equal(0, summary.PendingServiceFeeInvoices);
    }

    // ==================================================================
    // T7 - a payment order in any non-Paid state leaves the invoice pending
    // ==================================================================

    [Fact(DisplayName =
        "T7: a PaymentOrder in ANY non-Paid state leaves its Pending " +
        "invoice counted as pending")]
    public async Task NonPaidPaymentOrders_LeaveTheInvoicesPending()
    {
        await ResetBillingTablesAsync();

        // Every non-Paid order state in one scenario. A created, pending,
        // cancelled, failed or refunded order does not pay an invoice and does
        // not erase it, so all five invoices stay outstanding.
        var orderStates = new[]
        {
            PaymentStatus.Created,
            PaymentStatus.Pending,
            PaymentStatus.Cancelled,
            PaymentStatus.Failed,
            PaymentStatus.Refunded
        };

        await WithScopeDbContextAsync(db =>
        {
            foreach (var orderStatus in orderStates)
            {
                var invoice = NewRetainerInvoice(
                    RetainerInvoiceStatus.Pending);

                db.RetainerInvoices.Add(invoice);
                AddPaymentOrderForRetainer(db, invoice, orderStatus);
            }

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(orderStates.Length, summary.PendingRetainerInvoices);
    }

    // ==================================================================
    // T8 / T9 - invoice status is authoritative, both directions
    // ==================================================================

    [Fact(DisplayName =
        "T8: a Paid invoice with a Paid payment order is not pending")]
    public async Task APaidInvoiceAndOrder_IsNotPending()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            var invoice = NewRetainerInvoice(RetainerInvoiceStatus.Paid);

            db.RetainerInvoices.Add(invoice);
            AddPaymentOrderForRetainer(db, invoice, PaymentStatus.Paid);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(0, summary.PendingRetainerInvoices);
    }

    [Fact(DisplayName =
        "T9: a Paid payment order does NOT make its still-Pending invoice " +
        "disappear from the pending count")]
    public async Task APaidPaymentOrder_DoesNotOverrideInvoicePending()
    {
        // The inverse of T8, and the stronger statement: the summary must read
        // the invoice and never infer invoice state from payment_orders. Only
        // the invoice transition moves an invoice out of pending.
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            var invoice = NewRetainerInvoice(
                RetainerInvoiceStatus.Pending);

            db.RetainerInvoices.Add(invoice);
            AddPaymentOrderForRetainer(db, invoice, PaymentStatus.Paid);

            db.SaveChanges();
        });

        var summary = await ReadSummaryAsync();

        Assert.Equal(1, summary.PendingRetainerInvoices);
    }

    // ==================================================================
    // T10 - the revenue endpoint is untouched
    // ==================================================================

    [Fact(DisplayName =
        "T10: revenue counts only Paid invoices settled inside the window, " +
        "and the week and month windows differ")]
    public async Task RevenueInvoiceCount_CountsOnlyPaidInvoicesInPeriod()
    {
        // GetRevenueAsync uses a ROLLING window: week = UtcNow - 7 days and
        // month = UtcNow - 1 month. This test straddles that boundary on
        // purpose, so it would fail if either predicate were narrowed to the
        // current calendar week or month.
        await ResetBillingTablesAsync();

        var now = DateTime.UtcNow;

        await WithScopeDbContextAsync(db =>
        {
            // Paid 5 days ago -> inside week AND month. (2)
            db.RetainerInvoices.Add(NewRetainerInvoice(
                RetainerInvoiceStatus.Paid, paidAt: now.AddDays(-5)));
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Paid,
                paidAt: now.AddDays(-5));

            // Paid 20 days ago -> inside month, OUTSIDE week. (2)
            db.RetainerInvoices.Add(NewRetainerInvoice(
                RetainerInvoiceStatus.Paid, paidAt: now.AddDays(-20)));
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Paid,
                paidAt: now.AddDays(-20));

            // Paid 90 days ago -> outside both windows.
            db.RetainerInvoices.Add(NewRetainerInvoice(
                RetainerInvoiceStatus.Paid, paidAt: now.AddDays(-90)));

            // Non-Paid invoices must be excluded by STATUS even when they
            // carry a PaidAt inside the window, so revenue cannot be inflated
            // by a partially-populated row.
            db.RetainerInvoices.Add(NewRetainerInvoice(
                RetainerInvoiceStatus.Pending, paidAt: now.AddDays(-5)));
            db.RetainerInvoices.Add(NewRetainerInvoice(
                RetainerInvoiceStatus.Void, paidAt: now.AddDays(-5)));

            // A soft-deleted Paid invoice inside the window is excluded.
            db.RetainerInvoices.Add(NewRetainerInvoice(
                RetainerInvoiceStatus.Paid, isDeleted: true,
                paidAt: now.AddDays(-5)));

            db.SaveChanges();
        });

        var month = await ReadRevenueAsync("month");
        var week = await ReadRevenueAsync("week");

        Assert.Equal(4, month.InvoiceCount);
        Assert.Equal(2, week.InvoiceCount);
    }

    // ==================================================================
    // T11 - the non-payment KPIs are unaffected by invoice state
    // ==================================================================

    [Fact(DisplayName =
        "T11: the non-payment summary KPIs are identical before and after " +
        "invoice state changes")]
    public async Task NonPaymentKpis_AreUnaffectedByInvoiceState()
    {
        await ResetBillingTablesAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddRetainerInvoice(db, RetainerInvoiceStatus.Pending);
            AddServiceFeeInvoice(db, ServiceFeeInvoiceStatus.Pending);

            db.SaveChanges();
        });

        var before = await ReadSummaryAsync();

        // Drive the invoice KPIs to a very different value while leaving
        // clients, entities, employees and renewal tasks alone.
        await WithScopeDbContextAsync(db =>
        {
            foreach (var invoice in db.RetainerInvoices.ToList())
            {
                invoice.Status = RetainerInvoiceStatus.Void;
            }

            foreach (var invoice in db.ServiceFeeInvoices.ToList())
            {
                invoice.Status = ServiceFeeInvoiceStatus.Paid;
            }

            db.SaveChanges();
        });

        var after = await ReadSummaryAsync();

        // The invoice KPIs really did move, so this comparison is meaningful.
        Assert.NotEqual(
            before.PendingRetainerInvoices,
            after.PendingRetainerInvoices);

        Assert.Equal(before.TotalClients, after.TotalClients);
        Assert.Equal(before.TotalEntities, after.TotalEntities);
        Assert.Equal(before.TotalEmployees, after.TotalEmployees);
        Assert.Equal(before.ActiveRenewalTasks, after.ActiveRenewalTasks);

        // The portfolio figures are real counts, not placeholders.
        Assert.True(before.TotalClients > 0);
        Assert.True(before.TotalEntities > 0);
        Assert.True(before.TotalEmployees > 0);
        Assert.True(before.ActiveRenewalTasks > 0);
    }
}
