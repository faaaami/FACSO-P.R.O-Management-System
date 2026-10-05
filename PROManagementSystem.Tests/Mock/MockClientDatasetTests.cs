using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Persistence.Seeders;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Mock;

/// <summary>
/// Verifies the realistic mock client company dataset
/// ("FAMIL TECHNOLOGY SOLUTIONS L.L.C", seeded by
/// MockClientCompanySeeder for the client faamil@gmail.com) that drives
/// manual end-to-end testing. Checks the seeded rows themselves, the client
/// tenant-scoped API surface, the Razorpay create-order flow against the fake
/// service, and idempotency on re-run.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MockClientDatasetTests : IntegrationTestBase
{
    private static readonly Guid ClientUserId =
        MockClientCompanySeeder.Ids.Client;

    private static readonly Guid CompanyF =
        MockClientCompanySeeder.Ids.Company;

    private static readonly Guid ClientUserB =
        TestDataSeeder.Ids.ClientUserB;

    private static readonly Guid CompanyB =
        TestDataSeeder.Ids.CompanyB;

    public MockClientDatasetTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static async Task<JsonArray?> DataItemsAsync(
        HttpResponseMessage response)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        return body["data"]!["items"]!.AsArray();
    }

    private static async Task<string> ErrorCodeAsync(
        HttpResponseMessage response)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        return (string)body["error"]!["code"]!;
    }

    private async Task<HttpResponseMessage> ClientGet(
        string url,
        Guid userId,
        Guid? clientCompanyId)
        => await ScopeClient.SendAsync(
            ScopeGet(url, userId, "Client",
                clientCompanyId?.ToString()));

    private async Task<HttpResponseMessage> ClientPost(
        string url,
        object? body,
        Guid userId,
        Guid? clientCompanyId)
        => await ScopeClient.SendAsync(
            ScopePost(url, body, userId, "Client",
                clientCompanyId?.ToString()));

    // ==================================================================
    // 1. faamil user + tenant association
    // ==================================================================

    [Fact(DisplayName =
        "D1: faamil is an active/approved/verified Client linked to the mock company")]
    public async Task Faamil_UserIsVerifiedClient()
    {
        await WithScopeDbContextAsync(db =>
        {
            var user = Assert.Single(db.Users, x =>
                x.Email == MockClientCompanySeeder.ClientEmail);

            Assert.Equal(ClientUserId, user.Id);
            Assert.Equal(UserRole.Client, user.Role);
            Assert.True(user.IsActive);
            Assert.True(user.IsApproved);
            Assert.True(user.EmailVerified);
            Assert.Equal(CompanyF, user.ClientCompanyId);
        });
    }

    // ==================================================================
    // 2. Company / entity / employees
    // ==================================================================

    [Fact(DisplayName =
        "D2: Mock company, entity and employees are present and linked")]
    public async Task Company_Entity_Employees_Linked()
    {
        await WithScopeDbContextAsync(db =>
        {
            var company = db.ClientCompanies.Single(x =>
                x.Id == CompanyF);
            Assert.Equal(MockClientCompanySeeder.CompanyName, company.CompanyName);
            Assert.Equal("TL-FAMIL-2026-001", company.TradeLicenseNumber);
            Assert.Equal("+971500000001", company.Phone);
            Assert.Equal("Dubai", company.Emirate);
            Assert.True(company.IsActive);

            var entity = db.ClientEntities.Single(x =>
                x.ClientCompanyId == CompanyF);
            Assert.Equal(MockClientCompanySeeder.EntityName, entity.EntityName);
            Assert.Equal("1234567", entity.TradeLicenseNumber);
            Assert.Equal("Dubai", entity.Emirate);
            Assert.True(entity.IsActive);

            var ahmed = db.Employees.Single(x =>
                x.ClientEntityId == entity.Id &&
                x.FullName == "Ahmed Rahman");
            Assert.Equal("P-FAMIL-0001", ahmed.PassportNumber);
            Assert.Equal("Software Engineer", ahmed.JobTitle);
            Assert.Equal("Indian", ahmed.Nationality);
            Assert.True(ahmed.IsActive);

            var omar = db.Employees.Single(x =>
                x.ClientEntityId == entity.Id &&
                x.FullName == "Omar Khan");
            Assert.Equal("P-FAMIL-0002", omar.PassportNumber);
            Assert.Equal("PRO Coordinator", omar.JobTitle);
            Assert.Equal("Pakistani", omar.Nationality);
            Assert.True(omar.IsActive);
        });
    }

    // ==================================================================
    // 3. Documents
    // ==================================================================

    [Fact(DisplayName =
        "D3: Eight mock documents with valid single-owner semantics and expiry spread")]
    public async Task Documents_ValidOwnership_AndExpirySpread()
    {
        await WithScopeDbContextAsync(db =>
        {
            var docs = db.Documents
                .Where(x =>
                    x.DocumentNumber.StartsWith("TR-FAMIL") ||
                    x.DocumentNumber.StartsWith("EC-FAMIL") ||
                    x.DocumentNumber.StartsWith("P-FAMIL") ||
                    x.DocumentNumber.StartsWith("V-FAMIL") ||
                    x.DocumentNumber.StartsWith("EI-FAMIL"))
                .ToList();

            Assert.Equal(8, docs.Count);

            var entityDocs = docs
                .Where(x => x.ClientEntityId == MockClientCompanySeeder.Ids.Entity)
                .ToList();
            var employeeDocs = docs
                .Where(x => !x.ClientEntityId.HasValue)
                .ToList();

            Assert.Equal(2, entityDocs.Count);
            Assert.Equal(6, employeeDocs.Count);

            Assert.All(docs, doc =>
                Assert.True(
                    doc.ClientEntityId.HasValue != doc.EmployeeId.HasValue,
                    $"Document {doc.Id} must have exactly one owner."));
            Assert.All(docs, doc => Assert.True(doc.IsActive));
            Assert.All(docs, doc =>
                Assert.Equal("application/pdf", doc.ContentType));
            Assert.All(docs, doc => Assert.NotNull(doc.FileUrl));

            Assert.Contains(docs, d =>
                d.Type == PROManagementSystem.Domain.Enums.DocumentType.TradeLicense);
            Assert.Contains(docs, d =>
                d.Type == PROManagementSystem.Domain.Enums.DocumentType.EstablishmentCard);
            Assert.Contains(docs, d =>
                d.Type == PROManagementSystem.Domain.Enums.DocumentType.Passport);
            Assert.Contains(docs, d =>
                d.Type == PROManagementSystem.Domain.Enums.DocumentType.Visa);
            Assert.Contains(docs, d =>
                d.Type == PROManagementSystem.Domain.Enums.DocumentType.EmiratesId);

            var today = DateTime.UtcNow.Date;
            var daysRemaining = docs
                .Where(d => d.ExpiryDate.HasValue)
                .Select(d => (d.ExpiryDate!.Value.Date - today).Days)
                .ToList();

            Assert.Contains(daysRemaining, d => d > 0 && d <= 30);
            Assert.Contains(daysRemaining, d => d > 30 && d <= 60);
            Assert.Contains(daysRemaining, d => d > 60 && d <= 90);
            Assert.Contains(daysRemaining, d => d > 90);
        });
    }

    // ==================================================================
    // 4. Contract
    // ==================================================================

    [Fact(DisplayName =
        "D4: Active mock retainer contract with expected terms")]
    public async Task Contract_Active_Expected()
    {
        await WithScopeDbContextAsync(db =>
        {
            var contract = db.ServiceContracts.Single(x =>
                x.Id == MockClientCompanySeeder.Ids.Contract);
            Assert.Equal(MockClientCompanySeeder.ContractNumber, contract.ContractNumber);
            Assert.Equal(CompanyF, contract.ClientCompanyId);
            Assert.Equal(PROManagementSystem.Domain.Enums.ServiceContractStatus.Active, contract.Status);
            Assert.Equal(1000m, contract.RetainerAmount);
            Assert.Equal(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                contract.StartDate);
            Assert.Equal(contract.EndDate,
                new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));
            Assert.False(string.IsNullOrWhiteSpace(contract.Terms));
        });
    }

    // ==================================================================
    // 5-7. Billing rows
    // ==================================================================

    [Fact(DisplayName =
        "D5: Pending retainer invoice linked to the mock company and contract")]
    public async Task RetainerInvoice_Pending_Linked()
    {
        await WithScopeDbContextAsync(db =>
        {
            var invoice = db.RetainerInvoices.Single(x =>
                x.Id == MockClientCompanySeeder.Ids.RetainerInvoice);
            Assert.Equal(MockClientCompanySeeder.RetainerInvoiceNumber, invoice.InvoiceNumber);
            Assert.Equal(CompanyF, invoice.ClientCompanyId);
            Assert.Equal(MockClientCompanySeeder.Ids.Contract, invoice.ServiceContractId);
            Assert.Equal(PROManagementSystem.Domain.Enums.RetainerInvoiceStatus.Pending, invoice.Status);
            Assert.Equal(1000m, invoice.Amount);
            Assert.Equal("AED", invoice.Currency);
            Assert.Null(invoice.PaidAt);
        });
    }

    [Fact(DisplayName =
        "D6: Pending service-fee invoice linked to the completed renewal task")]
    public async Task ServiceFeeInvoice_Pending_LinkedToCompletedTask()
    {
        await WithScopeDbContextAsync(db =>
        {
            var invoice = db.ServiceFeeInvoices.Single(x =>
                x.Id == MockClientCompanySeeder.Ids.ServiceFeeInvoice);
            Assert.Equal(MockClientCompanySeeder.ServiceFeeInvoiceNumber, invoice.InvoiceNumber);
            Assert.Equal(CompanyF, invoice.ClientCompanyId);
            Assert.Equal(MockClientCompanySeeder.Ids.TaskCompleted, invoice.RenewalTaskId);
            Assert.Equal(PROManagementSystem.Domain.Enums.ServiceFeeInvoiceStatus.Pending, invoice.Status);
            Assert.Equal(750m, invoice.Amount);
            Assert.Equal("AED", invoice.Currency);
            Assert.Null(invoice.PaidAt);

            var task = db.RenewalTasks.Single(x =>
                x.Id == MockClientCompanySeeder.Ids.TaskCompleted);
            Assert.Equal(PROManagementSystem.Domain.Enums.RenewalTaskStatus.Updated, task.Status);
            Assert.True(task.CompletedAt.HasValue);
            Assert.Equal(750m, task.ServiceFeeAmount);
        });
    }

    [Fact(DisplayName =
        "D7: Gov-fee disbursement present in a valid InvoicedToClient state")]
    public async Task GovFeeDisbursement_InvoicedToClient()
    {
        await WithScopeDbContextAsync(db =>
        {
            var disbursement = db.GovFeeDisbursements.Single(x =>
                x.Id == MockClientCompanySeeder.Ids.GovFeeDisbursement);
            Assert.Equal(CompanyF, disbursement.ClientCompanyId);
            Assert.Equal(MockClientCompanySeeder.Ids.TaskAssigned, disbursement.RenewalTaskId);
            Assert.Equal(MockClientCompanySeeder.GovernmentReference, disbursement.GovernmentReference);
            Assert.Equal(PROManagementSystem.Domain.Enums.GovFeeDisbursementStatus.InvoicedToClient, disbursement.Status);
            Assert.NotEqual(default(DateTime), disbursement.PaidByFirmAt);
            Assert.True(disbursement.InvoicedAt.HasValue);
            Assert.False(disbursement.ReimbursedAt.HasValue);
        });
    }

    // ==================================================================
    // 8. No payment orders / no paid invoices
    // ==================================================================

    [Fact(DisplayName =
        "D8: Seeder never creates payment orders and never marks invoices paid")]
    public async Task No_PaymentOrder_And_NoPaid_Invoices()
    {
        await WithScopeDbContextAsync(db =>
        {
            Assert.DoesNotContain(db.PaymentOrders.IgnoreQueryFilters(),
                x => x.ClientCompanyId == CompanyF);

            Assert.All(db.RetainerInvoices.IgnoreQueryFilters()
                .Where(x => x.ClientCompanyId == CompanyF),
                invoice => Assert.Equal(
                    PROManagementSystem.Domain.Enums.RetainerInvoiceStatus.Pending,
                    invoice.Status));
            Assert.All(db.ServiceFeeInvoices.IgnoreQueryFilters()
                .Where(x => x.ClientCompanyId == CompanyF),
                invoice => Assert.Equal(
                    PROManagementSystem.Domain.Enums.ServiceFeeInvoiceStatus.Pending,
                    invoice.Status));
        });
    }

    // ==================================================================
    // 9-10. Tenant-scoped API surface
    // ==================================================================

    [Fact(DisplayName =
        "D9: faamil sees its own pending invoices through the client API")]
    public async Task Api_Faamil_SeesOwnInvoices()
    {
        var retainer = await ClientGet(
            "/api/v1/client/retainer-invoices",
            ClientUserId, CompanyF);
        Assert.Equal(HttpStatusCode.OK, retainer.StatusCode);

        var retainerItems = await DataItemsAsync(retainer);
        Assert.Contains(retainerItems!, item =>
            (string)item!["invoiceNumber"]! ==
            MockClientCompanySeeder.RetainerInvoiceNumber);

        var serviceFee = await ClientGet(
            "/api/v1/client/service-fee-invoices",
            ClientUserId, CompanyF);
        Assert.Equal(HttpStatusCode.OK, serviceFee.StatusCode);

        var serviceFeeItems = await DataItemsAsync(serviceFee);
        Assert.Contains(serviceFeeItems!, item =>
            (string)item!["invoiceNumber"]! ==
            MockClientCompanySeeder.ServiceFeeInvoiceNumber);
    }

    [Fact(DisplayName =
        "D10: Another company's client cannot see the mock company's invoices")]
    public async Task Api_CrossCompany_Invisible()
    {
        var retainer = await ClientGet(
            "/api/v1/client/retainer-invoices",
            ClientUserB, CompanyB);
        Assert.Equal(HttpStatusCode.OK, retainer.StatusCode);

        var items = await DataItemsAsync(retainer);
        Assert.DoesNotContain(items!, item =>
            (string)item!["invoiceNumber"]! ==
            MockClientCompanySeeder.RetainerInvoiceNumber);

        var byId = await ClientGet(
            $"/api/v1/client/retainer-invoices/{MockClientCompanySeeder.Ids.RetainerInvoice}",
            ClientUserB, CompanyB);
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
        Assert.Equal("NOT_FOUND", await ErrorCodeAsync(byId));
    }

    // ==================================================================
    // 11. Create-order flow via FakeRazorpayService
    // ==================================================================

    [Fact(DisplayName =
        "D11: faamil creates a Razorpay order for the pending retainer invoice (200)")]
    public async Task Api_CreateOrder_ForMockRetainer_Ok()
    {
        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = MockClientCompanySeeder.Ids.RetainerInvoice
            },
            ClientUserId, CompanyF);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        var data = body["data"]!;

        Assert.Equal("Pending", (string)data["status"]!);
        Assert.Equal(FakeRazorpayService.FakeOrderId,
            (string)data["razorpayOrderId"]!);
        Assert.Equal(FakeRazorpayService.FakeKeyId,
            (string)data["razorpayKeyId"]!);
        Assert.Equal(1000m * 100, (int)data["razorpayAmount"]!);
        Assert.Equal(
            MockClientCompanySeeder.Ids.RetainerInvoice.ToString(),
            data["retainerInvoiceId"]!.ToString());

        await WithScopeDbContextAsync(db =>
        {
            var order = Assert.Single(db.PaymentOrders,
                x => x.ClientCompanyId == CompanyF);
            Assert.Equal(MockClientCompanySeeder.Ids.RetainerInvoice,
                order.RetainerInvoiceId);
            Assert.Equal(
                PROManagementSystem.Domain.Enums.PaymentStatus.Pending,
                order.Status);

            var invoice = db.RetainerInvoices.Single(x =>
                x.Id == MockClientCompanySeeder.Ids.RetainerInvoice);
            Assert.Equal(
                PROManagementSystem.Domain.Enums.RetainerInvoiceStatus.Pending,
                invoice.Status);
            Assert.Null(invoice.PaidAt);
        });
    }

    // ==================================================================
    // 12. Idempotency
    // ==================================================================

    [Fact(DisplayName =
        "D12: Re-running the seeder inserts nothing and stays consistent")]
    public async Task Seeder_IsIdempotent()
    {
        await WithScopeDbContextAsync(db =>
        {
            var before = CountMockRows(db);

            MockClientCompanySeeder.Seed(db);

            var after = CountMockRows(db);
            Assert.Equal(before, after);
        });
    }

    // ==================================================================
    // 13. Re-association of a pre-existing faamil account
    // ==================================================================

    [Fact(DisplayName =
        "D13: Re-seeding re-associates a pre-existing faamil account (local DB scenario)")]
    public async Task Seeder_RepairsPreExistingFaamilAssociation()
    {
        await WithScopeDbContextAsync(db =>
        {
            // Reproduce the local dev bug: faamil existed before the seeder,
            // so its row had client_company_id = NULL (no duplicate row is
            // created because the email already exists).
            db.Database.ExecuteSqlRaw(
                "UPDATE users SET client_company_id = NULL" +
                " WHERE email = {0}",
                MockClientCompanySeeder.ClientEmail);

            MockClientCompanySeeder.Seed(db);

            var rows = db.Users.IgnoreQueryFilters()
                .Where(x => x.Email == MockClientCompanySeeder.ClientEmail)
                .ToList();
            var row = Assert.Single(rows);
            Assert.Equal(MockClientCompanySeeder.Ids.Client, row.Id);
            Assert.Equal(MockClientCompanySeeder.Ids.Company, row.ClientCompanyId);

            MockClientCompanySeeder.Seed(db);

            Assert.Single(db.Users.IgnoreQueryFilters(),
                x => x.Email == MockClientCompanySeeder.ClientEmail);
        });
    }

    private static (int Users, int Documents, int Tasks, int RetInvoices, int SfInvoices, int GovFees) CountMockRows(
        ApplicationDbContext db)
    {
        var userEmails = new[]
        {
            MockClientCompanySeeder.ClientEmail,
            MockClientCompanySeeder.AgentEmail
        };

        return (
            db.Users.IgnoreQueryFilters().Count(x =>
                userEmails.Contains(x.Email)),
            db.Documents.IgnoreQueryFilters().Count(x =>
                x.DocumentNumber.StartsWith("TR-FAMIL") ||
                x.DocumentNumber.StartsWith("EC-FAMIL") ||
                x.DocumentNumber.StartsWith("P-FAMIL") ||
                x.DocumentNumber.StartsWith("V-FAMIL") ||
                x.DocumentNumber.StartsWith("EI-FAMIL")),
            db.RenewalTasks.IgnoreQueryFilters().Count(x =>
                MockClientCompanySeeder.Ids.TaskSubmitted == x.Id ||
                MockClientCompanySeeder.Ids.TaskAssigned == x.Id ||
                MockClientCompanySeeder.Ids.TaskCompleted == x.Id),
            db.RetainerInvoices.IgnoreQueryFilters().Count(x =>
                x.InvoiceNumber == MockClientCompanySeeder.RetainerInvoiceNumber),
            db.ServiceFeeInvoices.IgnoreQueryFilters().Count(x =>
                x.InvoiceNumber == MockClientCompanySeeder.ServiceFeeInvoiceNumber),
            db.GovFeeDisbursements.IgnoreQueryFilters().Count(x =>
                x.GovernmentReference == MockClientCompanySeeder.GovernmentReference));
    }
}