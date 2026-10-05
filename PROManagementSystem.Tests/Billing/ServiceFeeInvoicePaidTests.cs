using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// D3-6: every branch of PATCH /api/v1/service-fee-invoices/{id}/mark-paid.
///
/// The handler is deliberately the most intricate write in the billing area:
/// it opens a transaction, resolves the related PaymentOrder, takes a
/// FOR UPDATE lock on the order row first and then on the invoice row (in that
/// order, to match the webhook handler and avoid deadlocks), re-checks the
/// status AFTER the locks, writes the invoice, the order and one audit row,
/// then commits.
///
/// Each test below targets a specific branch: the happy path, each rejection,
/// the order-synchronisation behaviour, the row-lock serialisation, and the
/// interaction with the webhook that shares those locks.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ServiceFeeInvoicePaidTests : IntegrationTestBase
{
    private const decimal Amount = 3200.00m;

    public ServiceFeeInvoicePaidTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<Guid> SeedTaskAsync()
    {
        var taskId = Guid.NewGuid();

        // The unique filtered index permits only one non-terminal task per
        // document, so each task needs a document of its own rather than a
        // shared seeded one.
        var documentId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber =
                    $"DOC-MARKPAID-{documentId.ToString("N")[..8]}",
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(20),
                FileUrl = $"https://storage.test/{documentId}",
                FileName = "passport.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = taskId,
                DocumentId = documentId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = TestDataSeeder.Ids.AgentA,
                Status = RenewalTaskStatus.Approved,
                ServiceFeeAmount = Amount,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return taskId;
    }

    private async Task<Guid> SeedInvoiceAsync(
        ServiceFeeInvoiceStatus status = ServiceFeeInvoiceStatus.Pending,
        bool isDeleted = false,
        string? invoiceNumber = null,
        Guid? renewalTaskId = null)
    {
        var invoiceId = Guid.NewGuid();
        var taskId = renewalTaskId ?? await SeedTaskAsync();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.ServiceFeeInvoices.Add(new ServiceFeeInvoice
            {
                Id = invoiceId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                RenewalTaskId = taskId,
                InvoiceNumber =
                    invoiceNumber ??
                    $"SFI-T-{invoiceId.ToString("N")[..10]}",
                InvoiceDate = now,
                DueDate = now.AddDays(30),
                Amount = Amount,
                Currency = "AED",
                Status = status,
                Description = "Seed",
                PaidAt = status == ServiceFeeInvoiceStatus.Paid
                    ? now.AddHours(-1)
                    : null,
                VoidedAt = status == ServiceFeeInvoiceStatus.Void
                    ? now.AddHours(-1)
                    : null,
                VoidReason = status == ServiceFeeInvoiceStatus.Void
                    ? "Seeded as void"
                    : null,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now : null
            });

            db.SaveChanges();
        });

        return invoiceId;
    }

    private async Task<PaymentOrder> SeedOrderAsync(
        Guid invoiceId,
        PaymentStatus status,
        string razorpayOrderId,
        bool isDeleted = false)
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            InvoiceType = PaymentInvoiceType.ServiceFeeInvoice,
            ServiceFeeInvoiceId = invoiceId,
            RetainerInvoiceId = null,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            Amount = Amount,
            Currency = "AED",
            Status = status,
            RazorpayOrderId = razorpayOrderId,
            RazorpayPaymentId = null,
            RazorpaySignature = null,
            PaidAt = status == PaymentStatus.Paid
                ? DateTime.UtcNow.AddHours(-1)
                : null,
            FailedAt = null,
            CancelledAt = null,
            Notes = "Seeded",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(order);
            db.SaveChanges();
        });

        return order;
    }

    private Task<HttpResponseMessage> MarkPaidAsync(
        Guid invoiceId,
        Guid? asUser = null,
        string role = "Admin")
        => ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/service-fee-invoices/{invoiceId}/mark-paid",
            null,
            asUser ?? TestDataSeeder.Ids.Admin,
            role));

    private async Task<ServiceFeeInvoice> ReadInvoiceAsync(
        Guid invoiceId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.ServiceFeeInvoices
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == invoiceId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<PaymentOrder?> ReadOrderForInvoiceAsync(
        Guid invoiceId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.PaymentOrders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    x => x.ServiceFeeInvoiceId == invoiceId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<int> PaidAuditCountAsync(Guid invoiceId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            // EntityId is the invoice, so this counts only this invoice's
            // payment audits and ignores everything the shared seed planted.
            return await db.AuditLogs
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.Action == "ServiceFeeInvoicePaid" &&
                    x.EntityId == invoiceId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private static void AssertStatus(
        HttpResponseMessage response,
        HttpStatusCode expected,
        string stage)
    {
        Assert.True(
            response.StatusCode == expected,
            $"{stage}: expected {(int)expected} " +
            $"{expected} but got {(int)response.StatusCode}.");
    }

    private static string Signature(string rawBody)
    {
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(
                FakeRazorpayService.FakeWebhookSecret));

        return Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();
    }

    private Task<HttpResponseMessage> PostCapturedWebhookAsync(
        string razorpayOrderId,
        int amountInSmallestUnit)
    {
        var rawBody =
            "{\"event\":\"payment.captured\"," +
            "\"payload\":{\"payment\":{\"entity\":{" +
            "\"id\":\"pay_markpaid_01\"," +
            "\"order_id\":\"" + razorpayOrderId + "\"," +
            "\"amount\":" + amountInSmallestUnit + "," +
            "\"currency\":\"AED\"," +
            "\"status\":\"captured\"}}}}";

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/client/payments/webhook")
        {
            Content = new StringContent(
                rawBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(
            "X-Razorpay-Signature", Signature(rawBody));

        return ScopeClient.SendAsync(request);
    }

    private async Task<(Guid orderId, string razorpayOrderId, int amount)>
        CreateGatewayOrderAsync(Guid invoiceId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/client/payments/create-order")
        {
            Content = JsonContent.Create(new
            {
                invoiceType = "ServiceFee",
                invoiceId
            })
        };

        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                TestDataSeeder.Ids.ClientUserA,
                "Client",
                TestDataSeeder.Ids.CompanyA.ToString()));

        var response = await ScopeClient.SendAsync(request);
        AssertStatus(
            response, HttpStatusCode.OK, "create-order");

        var data = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!["data"]!;

        return (
            Guid.Parse(data["id"]!.ToString()),
            (string)data["razorpayOrderId"]!,
            (int)data["razorpayAmount"]!);
    }

    // ==================================================================
    // 1. The happy path
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.1: marking a pending invoice paid returns 200, stamps PaidAt and writes one audit")]
    public async Task PendingInvoice_IsMarkedPaid()
    {
        var invoiceId = await SeedInvoiceAsync();

        var response = await MarkPaidAsync(invoiceId);
        var body = await response.Content.ReadAsStringAsync();

        AssertStatus(response, HttpStatusCode.OK, "mark-paid");
        Assert.Contains("\"status\":\"Paid\"", body);

        var invoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);
        Assert.Equal(1, await PaidAuditCountAsync(invoiceId));

        // There was no order to keep in step.
        Assert.Null(await ReadOrderForInvoiceAsync(invoiceId));
    }

    // ==================================================================
    // 2. Already paid
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.2: marking an already paid invoice again is a 409 and does not restamp it or re-audit")]
    public async Task AlreadyPaid_IsRejected()
    {
        var invoiceId = await SeedInvoiceAsync();
        var first = await MarkPaidAsync(invoiceId);
        AssertStatus(first, HttpStatusCode.OK, "first mark-paid");

        var paidAtAfterFirst = (await ReadInvoiceAsync(invoiceId))
            .PaidAt;

        var second = await MarkPaidAsync(invoiceId);
        var body = await second.Content.ReadAsStringAsync();

        AssertStatus(second, HttpStatusCode.Conflict, "second");
        Assert.Contains("Only pending invoices", body);

        // The original settlement is untouched: no restamp, no second audit.
        Assert.Equal(
            paidAtAfterFirst,
            (await ReadInvoiceAsync(invoiceId)).PaidAt);
        Assert.Equal(1, await PaidAuditCountAsync(invoiceId));
    }

    // ==================================================================
    // 3. Voided
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.3: marking a voided invoice paid is a 409 and leaves it voided")]
    public async Task VoidedInvoice_IsRejected()
    {
        var invoiceId = await SeedInvoiceAsync(
            ServiceFeeInvoiceStatus.Void);

        var response = await MarkPaidAsync(invoiceId);
        var body = await response.Content.ReadAsStringAsync();

        AssertStatus(response, HttpStatusCode.Conflict, "mark-paid");
        Assert.Contains("Only pending invoices", body);

        var invoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Void, invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Equal(0, await PaidAuditCountAsync(invoiceId));
    }

    // ==================================================================
    // 4. Unknown invoice
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.4: marking an unknown invoice paid is a 404")]
    public async Task UnknownInvoice_IsNotFound()
    {
        var response = await MarkPaidAsync(Guid.NewGuid());
        var body = await response.Content.ReadAsStringAsync();

        AssertStatus(
            response, HttpStatusCode.NotFound, "mark-paid");
        Assert.Contains("was not found", body);
    }

    // ==================================================================
    // 5. Soft-deleted invoice
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.5: marking a soft-deleted invoice paid is a 404 and does not resurrect it")]
    public async Task SoftDeletedInvoice_IsNotFound()
    {
        var invoiceId = await SeedInvoiceAsync(isDeleted: true);

        var response = await MarkPaidAsync(invoiceId);

        AssertStatus(
            response, HttpStatusCode.NotFound, "mark-paid");

        var invoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Equal(0, await PaidAuditCountAsync(invoiceId));
    }

    // ==================================================================
    // 6. A pending order is settled with the invoice
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.6: marking paid also settles the invoice's pending payment order")]
    public async Task PendingOrder_IsSettledWithTheInvoice()
    {
        var invoiceId = await SeedInvoiceAsync();
        var order = await SeedOrderAsync(
            invoiceId, PaymentStatus.Pending, "order_markpaid_06");

        var response = await MarkPaidAsync(invoiceId);
        AssertStatus(response, HttpStatusCode.OK, "mark-paid");

        var settled = await ReadOrderForInvoiceAsync(invoiceId);

        Assert.NotNull(settled);
        Assert.Equal(order.Id, settled!.Id);
        Assert.Equal(PaymentStatus.Paid, settled.Status);
        Assert.NotNull(settled.PaidAt);

        // The invoice agrees with its order.
        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid,
            (await ReadInvoiceAsync(invoiceId)).Status);
    }

    // ==================================================================
    // 7. A soft-deleted order is ignored, an already paid one is adopted
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.7: a soft-deleted order is ignored, and settling an already paid order preserves its original PaidAt")]
    public async Task OrderStates_AreRespected()
    {
        // 7a. A soft-deleted order must not be dragged into the settlement.
        var deletedInvoiceId = await SeedInvoiceAsync();
        await SeedOrderAsync(
            deletedInvoiceId,
            PaymentStatus.Pending,
            "order_markpaid_07a",
            isDeleted: true);

        var deletedResponse =
            await MarkPaidAsync(deletedInvoiceId);

        AssertStatus(
            deletedResponse, HttpStatusCode.OK, "7a mark-paid");

        var ignoredOrder =
            await ReadOrderForInvoiceAsync(deletedInvoiceId);

        Assert.NotNull(ignoredOrder);
        Assert.Equal(
            PaymentStatus.Pending, ignoredOrder!.Status);
        Assert.Null(ignoredOrder.PaidAt);

        // 7b. An already paid order is adopted but keeps its ORIGINAL capture
        //     timestamp. PaidAt records when the order was first settled, so
        //     D4 guards the assignment in step 7 of the handler and only
        //     stamps an order that has never been paid. The invoice still
        //     settles normally, and the order's first-settlement time is
        //     preserved rather than replaced by the manual settlement time.
        var paidInvoiceId = await SeedInvoiceAsync();
        await SeedOrderAsync(
            paidInvoiceId, PaymentStatus.Paid, "order_markpaid_07b");

        var orderPaidAtBefore = (await ReadOrderForInvoiceAsync(
            paidInvoiceId))!.PaidAt;

        Assert.NotNull(orderPaidAtBefore);

        var paidResponse = await MarkPaidAsync(paidInvoiceId);
        AssertStatus(paidResponse, HttpStatusCode.OK, "7b mark-paid");

        var adopted =
            await ReadOrderForInvoiceAsync(paidInvoiceId);

        Assert.Equal(PaymentStatus.Paid, adopted!.Status);

        // The order was already settled, so its PaidAt must be untouched.
        Assert.Equal(
            orderPaidAtBefore,
            adopted.PaidAt);

        // The invoice settles for the first time here, so it does get stamped.
        var settledInvoice = await ReadInvoiceAsync(paidInvoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid, settledInvoice.Status);
        Assert.NotNull(settledInvoice.PaidAt);

        // Exactly one settlement audit for the invoice.
        Assert.Equal(1, await PaidAuditCountAsync(paidInvoiceId));
    }

    // ==================================================================
    // 8. Concurrent manual mark-paid calls
    // ==================================================================

    /// <summary>
    /// The FOR UPDATE lock on the invoice row is what makes this safe. Both
    /// callers pass the initial existence check, then queue on the lock; the
    /// second one to acquire it re-reads the state and finds the invoice no
    /// longer Pending. Exactly one must win, and the loser must not leave a
    /// second audit row behind.
    /// </summary>
    [Fact(DisplayName =
        "D3-6.8: two concurrent mark-paid calls settle the invoice exactly once")]
    public async Task ConcurrentMarkPaid_ExactlyOneWins()
    {
        var invoiceId = await SeedInvoiceAsync();

        var responses = await Task.WhenAll(
            MarkPaidAsync(invoiceId),
            MarkPaidAsync(invoiceId));

        Assert.Single(
            responses,
            r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(
            responses,
            r => r.StatusCode == HttpStatusCode.Conflict);

        var invoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);

        Assert.Equal(1, await PaidAuditCountAsync(invoiceId));
    }

    // ==================================================================
    // 9. Manual mark-paid racing the webhook
    // ==================================================================

    /// <summary>
    /// The webhook handler locks the order and then the invoice in the same
    /// order this handler does, so the two must not deadlock and must not both
    /// write. Whichever arrives first settles the pair; the other observes a
    /// non-Pending invoice and backs off.
    /// </summary>
    [Fact(DisplayName =
        "D3-6.9: a manual mark-paid racing a captured webhook leaves the invoice and its order consistently paid")]
    public async Task ManualMarkPaid_RacingTheWebhook_IsConsistent()
    {
        var invoiceId = await SeedInvoiceAsync();

        var (_, razorpayOrderId, gatewayAmount) =
            await CreateGatewayOrderAsync(invoiceId);

        var manual = MarkPaidAsync(invoiceId);
        var webhook = PostCapturedWebhookAsync(
            razorpayOrderId, gatewayAmount);

        var responses = await Task.WhenAll(manual, webhook);

        // The manual call either wins the race or is refused because the
        // webhook already settled the invoice. A deadlock would surface here
        // as an unexpected status, so assert the permitted pair only.
        Assert.Contains(
            responses,
            r => r.StatusCode == HttpStatusCode.OK);

        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Conflict,
                "Unexpected status " +
                $"{(int)response.StatusCode} from the race.");
        }

        // Either way the end state is the same and internally consistent.
        var invoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);

        var order = await ReadOrderForInvoiceAsync(invoiceId);
        Assert.NotNull(order);
        Assert.Equal(PaymentStatus.Paid, order!.Status);
        Assert.NotNull(order.PaidAt);

        // At most one manual settlement audit, whichever order won.
        Assert.True(
            await PaidAuditCountAsync(invoiceId) <= 1);

        // And the pair is settled once and stays settled: replaying both
        // paths is a clean no-op.
        AssertStatus(
            await MarkPaidAsync(invoiceId),
            HttpStatusCode.Conflict,
            "replay mark-paid");
        AssertStatus(
            await PostCapturedWebhookAsync(
                razorpayOrderId, gatewayAmount),
            HttpStatusCode.OK,
            "replay webhook");

        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid,
            (await ReadInvoiceAsync(invoiceId)).Status);
    }

    // ==================================================================
    // 11. Cancelled order
    // ==================================================================

    /// <summary>
    /// A cancelled payment order is terminal, so manual mark-paid must refuse
    /// to resurrect it. The handler checks the order status AFTER the order
    /// row lock is held, so nothing is settled and the invoice is left pending
    /// for a fresh payment attempt.
    /// </summary>
    /// <remarks>
    /// This inverts the previous expectation, which asserted the handler's
    /// former unconditional Paid assignment revived a cancelled order and
    /// stamped a first PaidAt on it.
    /// </remarks>
    [Fact(DisplayName =
        "D4-6.11: settling a pending invoice whose order was cancelled is 409 and settles nothing")]
    public async Task CancelledOrder_RefusesToSettle()
    {
        var invoiceId = await SeedInvoiceAsync();
        var order = await SeedOrderAsync(
            invoiceId, PaymentStatus.Cancelled, "order_markpaid_11");

        // A cancelled order has never been paid, so it has no PaidAt.
        var orderPaidAtBefore = (await ReadOrderForInvoiceAsync(
            invoiceId))!.PaidAt;
        Assert.Null(orderPaidAtBefore);

        var response = await MarkPaidAsync(invoiceId);

        AssertStatus(response, HttpStatusCode.Conflict, "mark-paid");

        var untouched = await ReadOrderForInvoiceAsync(invoiceId);

        Assert.NotNull(untouched);
        Assert.Equal(order.Id, untouched!.Id);
        Assert.Equal(PaymentStatus.Cancelled, untouched.Status);
        Assert.Null(untouched.PaidAt);

        var invoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, invoice.Status);
        Assert.Null(invoice.PaidAt);

        Assert.Equal(0, await PaidAuditCountAsync(invoiceId));
    }

    // ==================================================================
    // 12. Split state: order already paid, invoice still pending
    // ==================================================================

    /// <summary>
    /// Neither settlement handler can produce this state - both write the
    /// order and the invoice in one transaction - so it is seeded directly.
    /// It is the only state in which the unconditional PaidAt assignment
    /// used to be observable, which is why D4 pins it explicitly.
    ///
    /// Before D4 the manual settlement replaced the order's original capture
    /// timestamp with the manual settlement time, destroying the record of
    /// when the payment was actually captured. After D4 the invoice settles
    /// for the first time while the order keeps the PaidAt it already had.
    /// </summary>
    [Fact(DisplayName =
        "D4-6.12: a split order-paid/invoice-pending state settles the invoice without overwriting the order's original PaidAt")]
    public async Task SplitState_SettlesInvoiceAndPreservesOrderPaidAt()
    {
        var invoiceId = await SeedInvoiceAsync();

        // Seeded with a known, deliberately distinctive capture time so an
        // accidental restamp cannot coincide with the original value.
        var originalOrderPaidAt =
            new DateTime(2024, 3, 17, 9, 41, 0, DateTimeKind.Utc);

        var order = await SeedOrderAsync(
            invoiceId, PaymentStatus.Paid, "order_markpaid_12");

        await WithScopeDbContextAsync(db =>
        {
            var stored = db.PaymentOrders
                .Single(x => x.Id == order.Id);

            stored.PaidAt = originalOrderPaidAt;
            stored.UpdatedAt = originalOrderPaidAt;

            db.SaveChanges();
        });

        // Confirm the split state that the handler will actually observe.
        var splitInvoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, splitInvoice.Status);
        Assert.Null(splitInvoice.PaidAt);

        var response = await MarkPaidAsync(invoiceId);
        AssertStatus(response, HttpStatusCode.OK, "mark-paid");

        // The invoice settles for the first time and is stamped.
        var settledInvoice = await ReadInvoiceAsync(invoiceId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid, settledInvoice.Status);
        Assert.NotNull(settledInvoice.PaidAt);

        // The order was already settled, so its PaidAt is preserved exactly.
        var settledOrder = await ReadOrderForInvoiceAsync(invoiceId);
        Assert.NotNull(settledOrder);
        Assert.Equal(PaymentStatus.Paid, settledOrder!.Status);
        Assert.Equal(
            originalOrderPaidAt,
            settledOrder.PaidAt!.Value.ToUniversalTime());

        // Status consistency is intact and the settlement is audited once.
        Assert.Equal(1, await PaidAuditCountAsync(invoiceId));
    }

    // ==================================================================
    // 13. Authorization
    // ==================================================================

    [Fact(DisplayName =
        "D3-6.10: mark-paid is admin-only; an Agent and a Client are refused and nothing changes")]
    public async Task NonAdmins_AreForbidden()
    {
        var agentInvoiceId = await SeedInvoiceAsync();
        var clientInvoiceId = await SeedInvoiceAsync();

        var asAgent = await MarkPaidAsync(
            agentInvoiceId, TestDataSeeder.Ids.AgentA, "Agent");
        var asClient = await MarkPaidAsync(
            clientInvoiceId, TestDataSeeder.Ids.ClientUserA, "Client");

        AssertStatus(
            asAgent, HttpStatusCode.Forbidden, "agent");
        AssertStatus(
            asClient, HttpStatusCode.Forbidden, "client");

        // Neither invoice moved.
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending,
            (await ReadInvoiceAsync(agentInvoiceId)).Status);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending,
            (await ReadInvoiceAsync(clientInvoiceId)).Status);

        Assert.Equal(0, await PaidAuditCountAsync(agentInvoiceId));
        Assert.Equal(0, await PaidAuditCountAsync(clientInvoiceId));
    }
}
