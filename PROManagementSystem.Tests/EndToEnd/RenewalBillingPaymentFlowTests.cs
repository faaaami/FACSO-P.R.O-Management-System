using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.EndToEnd;

/// <summary>
/// D3-E2E: the whole renewal -> billing -> payment chain driven through the
/// real HTTP surface, with only the gateway faked.
///
/// The stages, in the order production runs them:
///   1. a document inside the 90-day expiry window is picked up by
///      <see cref="DocumentExpiryJob"/>, which creates one renewal task;
///   2. an Admin sets the task's service fee and assigns an Agent;
///   3. the Agent drives the task through the strict sequential status chain
///      to Updated, supplying the renewed document expiry;
///   4. completing the task publishes RenewalTaskCompletedEvent AFTER the
///      status row has already been committed, so the service fee invoice is
///      created out-of-band of the status change;
///   5. the client creates a payment order against that invoice and Razorpay
///      reports capture on the webhook, which settles both the order and the
///      invoice.
///
/// Stage 4 is deliberately asserted rather than assumed: the invoice is only
/// guaranteed to exist because the event handler runs inline. See
/// InvoiceCompletionEventFailureTests for what happens when it does not.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RenewalBillingPaymentFlowTests : IntegrationTestBase
{
    private static readonly Guid AutomationUserId = Guid.Parse(
        "00000000-0000-0000-0000-0000000000aa");

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid ClientUserA =
        TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid CompanyA =
        TestDataSeeder.Ids.CompanyA;

    /// <summary>
    /// Inside the 90-day window (89 days out) so the job claims it, with
    /// enough margin that the run time of the suite cannot push it past the
    /// boundary.
    /// </summary>
    private const decimal ServiceFee = 1500.00m;

    /// <summary>
    /// A fixed future date, matching RenewedExpiryDateTests. The renewed value
    /// is compared on its date portion so the assertion does not depend on the
    /// time of day the suite runs.
    /// </summary>
    private static readonly DateTime RenewedExpiry = new(
        2030, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    public RenewalBillingPaymentFlowTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task SeedAutomationUserAsync()
    {
        await WithScopeDbContextAsync(db =>
        {
            if (!db.Users.Any(u => u.Id == AutomationUserId))
            {
                db.Users.Add(new User
                {
                    Id = AutomationUserId,
                    FullName = "Renewal Automation",
                    Email = "automation@test.local",
                    PasswordHash = "not-used",
                    Role = UserRole.Admin,
                    IsActive = true,
                    IsApproved = true,
                    EmailVerified = true,
                    ClientCompanyId = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    DeletedAt = null
                });
                db.SaveChanges();
            }
        });
    }

    private async Task<Guid> SeedExpiringDocumentAsync(
        int daysUntilExpiry)
    {
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
                    $"DOC-E2E-{documentId.ToString("N")[..8]}",
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(daysUntilExpiry),
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

            db.SaveChanges();
        });

        return documentId;
    }

    /// <summary>
    /// Runs the real job. The shared seed also plants in-window documents, so
    /// the caller correlates by document id rather than counting tasks.
    /// </summary>
    private async Task RunExpiryJobAsync()
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var job = scope.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();

        await job.ExecuteAsync();
    }

    private async Task<RenewalTask> ReadTaskAsync(Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.RenewalTasks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == taskId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<RenewalTask> FindActiveTaskAsync(
        Guid documentId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.RenewalTasks
                .SingleAsync(x =>
                    x.DocumentId == documentId &&
                    !x.IsDeleted &&
                    x.Status != RenewalTaskStatus.Updated);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<List<ServiceFeeInvoice>> InvoicesForTaskAsync(
        Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.ServiceFeeInvoices
                .IgnoreQueryFilters()
                .Where(x => x.RenewalTaskId == taskId)
                .ToListAsync();
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<PaymentOrder> OrderForServiceFeeInvoiceAsync(
        Guid invoiceId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.PaymentOrders
                .IgnoreQueryFilters()
                .SingleAsync(x =>
                    x.ServiceFeeInvoiceId == invoiceId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<DateTime> ReadDocumentExpiryAsync(
        Guid documentId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            var expiry = await db.Documents
                .Where(x => x.Id == documentId)
                .Select(x => x.ExpiryDate)
                .FirstAsync();

            Assert.True(
                expiry.HasValue,
                "The document should carry an expiry date.");

            return expiry!.Value;
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private Task<HttpResponseMessage> SetServiceFeeAsync(
        Guid taskId)
        => ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/admin/tasks/{taskId}/service-fee",
            new { amount = ServiceFee },
            Admin,
            "Admin"));

    private Task<HttpResponseMessage> AssignAsync(
        Guid taskId)
        => ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/admin/tasks/{taskId}/assign",
            new { staffId = AgentA },
            Admin,
            "Admin"));

    private Task<HttpResponseMessage> AgentStatusAsync(
        Guid taskId,
        string status,
        string? renewedExpiryDate = null)
    {
        object body = renewedExpiryDate is null
            ? new { status }
            : new
            {
                status,
                renewedExpiryDate
            };

        return ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/tasks/{taskId}/status",
            body,
            AgentA,
            "Agent"));
    }

    /// <summary>
    /// Drives the strict sequential chain an Agent must follow to reach a
    /// state one step away from completion.
    /// </summary>
    private async Task DriveToApprovedAsync(Guid taskId)
    {
        foreach (var step in new[]
        {
            "FeePaid", "AwaitingApproval", "Approved"
        })
        {
            var response = await AgentStatusAsync(taskId, step);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private async Task<HttpResponseMessage> CreateOrderAsync(
        Guid invoiceId)
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
                ClientUserA,
                "Client",
                CompanyA.ToString()));

        return await ScopeClient.SendAsync(request);
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

    private async Task<HttpResponseMessage> PostCapturedWebhookAsync(
        string razorpayOrderId,
        string paymentId,
        int amountInSmallestUnit)
    {
        var rawBody =
            "{\"event\":\"payment.captured\"," +
            "\"payload\":{\"payment\":{\"entity\":{" +
            "\"id\":\"" + paymentId + "\"," +
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

        return await ScopeClient.SendAsync(request);
    }

    private static void AssertOk(
        HttpResponseMessage response,
        string stage)
    {
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        throw new Xunit.Sdk.XunitException(
            $"Stage '{stage}' expected 200 OK but returned " +
            $"{(int)response.StatusCode}.");
    }

    // ------------------------------------------------------------------
    // E2E-1: the full chain, document in the window to settled invoice
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D3-E2E-1: a document inside the expiry window runs the whole renewal, invoice and payment chain to a settled invoice")]
    public async Task ExpiringDocument_CompletesTheWholeChain()
    {
        // ---- 1. Job claims the document and creates exactly one task ----

        var documentId = await SeedExpiringDocumentAsync(89);
        await SeedAutomationUserAsync();

        await RunExpiryJobAsync();

        var task = await FindActiveTaskAsync(documentId);

        // The job created the task unattended, already attached to the
        // document's client company.
        Assert.Equal(documentId, task.DocumentId);
        Assert.Equal(CompanyA, task.ClientCompanyId);
        Assert.Equal(
            RenewalTaskStatus.Submitted, task.Status);
        Assert.Null(task.AssignedStaffId);
        Assert.Null(task.ServiceFeeAmount);

        // A task now exists, so a second job run must not add another.
        await RunExpiryJobAsync();

        var dbAfterSecondRun = await CreateScopeDbContextAsync();
        int taskCount;
        try
        {
            taskCount = await dbAfterSecondRun.RenewalTasks
                .CountAsync(x =>
                    x.DocumentId == documentId &&
                    !x.IsDeleted);
        }
        finally
        {
            await dbAfterSecondRun.DisposeAsync();
        }

        Assert.Equal(1, taskCount);

        // ---- 2. Admin sets the fee and assigns the agent ----

        AssertOk(
            await SetServiceFeeAsync(task.Id), "set-service-fee");
        AssertOk(
            await AssignAsync(task.Id), "assign");

        var assigned = await ReadTaskAsync(task.Id);
        Assert.Equal(ServiceFee, assigned.ServiceFeeAmount);
        Assert.Equal(AgentA, assigned.AssignedStaffId);

        // ---- 3. Agent drives the status chain to Updated ----

        await DriveToApprovedAsync(task.Id);

        var completed = await AgentStatusAsync(
            task.Id, "Updated", "2030-06-15");

        AssertOk(completed, "complete");

        var updated = await ReadTaskAsync(task.Id);
        Assert.Equal(
            RenewalTaskStatus.Updated, updated.Status);

        // The renewed expiry landed on the DOCUMENT the task points at, and
        // the document is no longer inside the window.
        var newExpiry = await ReadDocumentExpiryAsync(documentId);
        Assert.Equal(
            RenewedExpiry.Date, newExpiry.Date);

        Assert.True(
            newExpiry > DateTime.UtcNow.AddDays(89),
            "The renewed expiry should have moved the document out " +
            "of the expiry window.");

        // ---- 4. Completion created the service fee invoice ----

        var invoices = await InvoicesForTaskAsync(task.Id);
        var invoice = Assert.Single(invoices);

        Assert.Equal(CompanyA, invoice.ClientCompanyId);
        Assert.Equal(ServiceFee, invoice.Amount);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, invoice.Status);
        Assert.Null(invoice.PaidAt);

        // The auto-generated number is derived from the task id, which proves
        // the invoice came from the completion event and not from a manual
        // create call.
        var expectedNumber =
            $"SFI-{DateTime.UtcNow:yyyyMMdd}-" +
            $"{task.Id.ToString()[..8].ToUpper()}";

        Assert.Equal(expectedNumber, invoice.InvoiceNumber);

        // ---- 5. Client pays the invoice through the webhook ----

        var orderResponse = await CreateOrderAsync(invoice.Id);
        AssertOk(orderResponse, "create-order");

        var orderBody = JsonNode.Parse(
            await orderResponse.Content.ReadAsStringAsync())!["data"]!;

        var razorpayOrderId =
            (string)orderBody["razorpayOrderId"]!;
        var razorpayAmount = (int)orderBody["razorpayAmount"]!;

        Assert.False(string.IsNullOrWhiteSpace(razorpayOrderId));
        Assert.True(razorpayAmount > 0);

        var webhook = await PostCapturedWebhookAsync(
            razorpayOrderId,
            "pay_e2e_chain_01",
            razorpayAmount);

        AssertOk(webhook, "webhook-captured");

        // ---- 6. Both the order and the invoice are settled ----

        var settledInvoice = Assert.Single(
            await InvoicesForTaskAsync(task.Id));

        Assert.Equal(
            ServiceFeeInvoiceStatus.Paid,
            settledInvoice.Status);
        Assert.NotNull(settledInvoice.PaidAt);

        var paymentOrder =
            await OrderForServiceFeeInvoiceAsync(invoice.Id);

        Assert.Equal(
            PaymentStatus.Paid, paymentOrder.Status);
        Assert.NotNull(paymentOrder.PaidAt);
        Assert.Equal(invoice.Id, paymentOrder.ServiceFeeInvoiceId);
        Assert.Equal(
            razorpayOrderId, paymentOrder.RazorpayOrderId);
    }

    // ------------------------------------------------------------------
    // E2E-2: completing a task that has no service fee
    // ------------------------------------------------------------------

    /// <summary>
    /// A task can only reach Updated once a fee has been set: the status
    /// handler validates ServiceFeeAmount BEFORE saving, so a completion
    /// without one is refused with a conflict and leaves no trace. This test
    /// pins that atomicity down, and then shows the failure is recoverable -
    /// setting a fee and retrying completes normally and creates exactly one
    /// invoice.
    /// </summary>
    [Fact(DisplayName =
        "D3-E2E-2: completing a task with no service fee is refused atomically, and succeeds once a fee is set")]
    public async Task CompletionWithNoServiceFee_RefusedAtomically()
    {
        var documentId = await SeedExpiringDocumentAsync(89);
        await SeedAutomationUserAsync();

        await RunExpiryJobAsync();

        var task = await FindActiveTaskAsync(documentId);

        // Assign so the agent can act on it, but deliberately never set a fee.
        AssertOk(await AssignAsync(task.Id), "assign");

        await DriveToApprovedAsync(task.Id);

        var expiryBefore =
            await ReadDocumentExpiryAsync(documentId);

        var response = await AgentStatusAsync(
            task.Id, "Updated", "2030-06-15");

        var body = await response.Content.ReadAsStringAsync();

        // The caller is told the completion is not allowed yet.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ServiceFeeAmount", body);

        // Nothing at all was committed: the task is still one step away from
        // completion and the document expiry has not moved.
        var afterRefusal = await ReadTaskAsync(task.Id);

        Assert.Equal(
            RenewalTaskStatus.Approved, afterRefusal.Status);
        Assert.Null(afterRefusal.ServiceFeeAmount);

        Assert.Equal(
            expiryBefore,
            await ReadDocumentExpiryAsync(documentId));

        // No invoice was conjured up on the way to refusing.
        Assert.Empty(await InvoicesForTaskAsync(task.Id));

        // Refusing twice is still just a refusal.
        var secondRefusal = await AgentStatusAsync(
            task.Id, "Updated", "2030-06-15");

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondRefusal.StatusCode);
        Assert.Empty(await InvoicesForTaskAsync(task.Id));

        // ---- the refusal is recoverable, not a dead end ----

        AssertOk(
            await SetServiceFeeAsync(task.Id), "set-service-fee");

        AssertOk(
            await AgentStatusAsync(
                task.Id, "Updated", "2030-06-15"),
            "complete-after-fee");

        var completed = await ReadTaskAsync(task.Id);
        Assert.Equal(
            RenewalTaskStatus.Updated, completed.Status);

        Assert.Equal(
            RenewedExpiry.Date,
            (await ReadDocumentExpiryAsync(documentId)).Date);

        var invoice = Assert.Single(
            await InvoicesForTaskAsync(task.Id));

        Assert.Equal(ServiceFee, invoice.Amount);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, invoice.Status);
    }
}
