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
/// Verifies the Familfa mock client dataset
/// ("FAMILFA TECH SOLUTIONS L.L.C", seeded by MockFamilfaClientSeeder for the
/// client familfa@gmail.com). Asserts the seeded rows themselves, the fully
/// active/approved/verified client state (usable as a live demo account),
/// the absence of payment orders / paid invoices, and idempotency on re-run.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MockFamilfaClientDatasetTests : IntegrationTestBase
{
    private static readonly Guid ClientUserId =
        MockFamilfaClientSeeder.Ids.Client;

    private static readonly Guid CompanyG =
        MockFamilfaClientSeeder.Ids.Company;

    public MockFamilfaClientDatasetTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// Seeds the Familfa world after the A/B/F baseline so the shared
    /// TestDataSeeder (and the pagination counts it feeds) stays untouched.
    /// </summary>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await WithScopeDbContextAsync(db =>
            MockFamilfaClientSeeder.Seed(db));
    }

    // ==================================================================
    // 1. familfa user + tenant association
    // ==================================================================

    [Fact(DisplayName =
        "G1: familfa is a fully active/approved/verified Client linked to the mock company")]
    public async Task Familfa_UserIsVerifiedClient()
    {
        await WithScopeDbContextAsync(db =>
        {
            var user = Assert.Single(db.Users, x =>
                x.Email == MockFamilfaClientSeeder.ClientEmail);

            Assert.Equal(ClientUserId, user.Id);
            Assert.Equal(UserRole.Client, user.Role);
            Assert.True(user.IsActive);
            Assert.True(user.IsApproved);
            Assert.True(user.EmailVerified);
            Assert.Equal(CompanyG, user.ClientCompanyId);
        });
    }

    // ==================================================================
    // 2. Company / entity / employees
    // ==================================================================

    [Fact(DisplayName =
        "G2: Mock company, entity and employees are present and linked")]
    public async Task Company_Entity_Employees_Linked()
    {
        await WithScopeDbContextAsync(db =>
        {
            var company = db.ClientCompanies.Single(x =>
                x.Id == CompanyG);
            Assert.Equal(MockFamilfaClientSeeder.CompanyName, company.CompanyName);
            Assert.Equal("TL-FAMILFA-2026-001", company.TradeLicenseNumber);
            Assert.Equal("+971500000002", company.Phone);
            Assert.Equal("Sharjah", company.Emirate);
            Assert.True(company.IsActive);

            var entity = db.ClientEntities.Single(x =>
                x.ClientCompanyId == CompanyG);
            Assert.Equal(MockFamilfaClientSeeder.EntityName, entity.EntityName);
            Assert.Equal("1122334", entity.TradeLicenseNumber);
            Assert.Equal("Sharjah", entity.Emirate);
            Assert.True(entity.IsActive);

            var hassan = db.Employees.Single(x =>
                x.ClientEntityId == entity.Id &&
                x.FullName == "Hassan Ali");
            Assert.Equal("P-FAMILFA-0001", hassan.PassportNumber);
            Assert.Equal("Software Engineer", hassan.JobTitle);
            Assert.Equal("Indian", hassan.Nationality);
            Assert.True(hassan.IsActive);

            var fatima = db.Employees.Single(x =>
                x.ClientEntityId == entity.Id &&
                x.FullName == "Fatima Noor");
            Assert.Equal("P-FAMILFA-0002", fatima.PassportNumber);
            Assert.Equal("PRO Coordinator", fatima.JobTitle);
            Assert.Equal("Pakistani", fatima.Nationality);
            Assert.True(fatima.IsActive);
        });
    }

    // ==================================================================
    // 3. Documents
    // ==================================================================

    [Fact(DisplayName =
        "G3: Ten mock documents with valid single-owner semantics and expiry spread")]
    public async Task Documents_ValidOwnership_AndExpirySpread()
    {
        await WithScopeDbContextAsync(db =>
        {
            var docs = db.Documents
                .Where(x => x.DocumentNumber.StartsWith("TEST-"))
                .ToList();

            Assert.Equal(10, docs.Count);

            var entityDocs = docs
                .Where(x => x.ClientEntityId == MockFamilfaClientSeeder.Ids.Entity)
                .ToList();
            var employeeDocs = docs
                .Where(x => !x.ClientEntityId.HasValue)
                .ToList();

            Assert.Equal(2, entityDocs.Count);
            Assert.Equal(8, employeeDocs.Count);

            Assert.All(docs, doc =>
                Assert.True(
                    doc.ClientEntityId.HasValue != doc.EmployeeId.HasValue,
                    $"Document {doc.Id} must have exactly one owner."));
            Assert.All(docs, doc => Assert.True(doc.IsActive));
            Assert.All(docs, doc =>
                Assert.Equal("application/pdf", doc.ContentType));
            Assert.All(docs, doc => Assert.NotNull(doc.FileUrl));
            Assert.All(docs, doc => Assert.NotNull(doc.Details));

            Assert.Contains(docs, d =>
                d.Type == DocumentType.TradeLicense);
            Assert.Contains(docs, d =>
                d.Type == DocumentType.EstablishmentCard);
            Assert.Contains(docs, d =>
                d.Type == DocumentType.Passport);
            Assert.Contains(docs, d =>
                d.Type == DocumentType.Visa);
            Assert.Contains(docs, d =>
                d.Type == DocumentType.EmiratesId);
            Assert.Contains(docs, d =>
                d.Type == DocumentType.LaborCard);

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
        "G4: Active mock retainer contract with expected terms")]
    public async Task Contract_Active_Expected()
    {
        await WithScopeDbContextAsync(db =>
        {
            var contract = db.ServiceContracts.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.Contract);
            Assert.Equal(MockFamilfaClientSeeder.ContractNumber, contract.ContractNumber);
            Assert.Equal(CompanyG, contract.ClientCompanyId);
            Assert.Equal(ServiceContractStatus.Active, contract.Status);
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
    // 5. Renewal tasks + histories
    // ==================================================================

    [Fact(DisplayName =
        "G5: Renewal tasks linked to expiring documents with histories")]
    public async Task RenewalTasks_LinkedToDocuments_WithHistories()
    {
        await WithScopeDbContextAsync(db =>
        {
            var submitted = db.RenewalTasks.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.TaskSubmitted);
            Assert.Equal(MockFamilfaClientSeeder.Ids.HassanPassport,
                submitted.DocumentId);
            Assert.Equal(CompanyG, submitted.ClientCompanyId);
            Assert.Equal(RenewalTaskStatus.Submitted, submitted.Status);
            Assert.Null(submitted.AssignedStaffId);

            var assigned = db.RenewalTasks.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.TaskAssigned);
            Assert.Equal(MockFamilfaClientSeeder.Ids.HassanVisa,
                assigned.DocumentId);
            Assert.Equal(RenewalTaskStatus.Submitted, assigned.Status);
            Assert.Equal(MockFamilfaClientSeeder.Ids.Agent,
                assigned.AssignedStaffId);

            var completed = db.RenewalTasks.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.TaskCompleted);
            Assert.Equal(MockFamilfaClientSeeder.Ids.HassanLaborCard,
                completed.DocumentId);
            Assert.Equal(RenewalTaskStatus.Updated, completed.Status);
            Assert.Equal(750m, completed.ServiceFeeAmount);
            Assert.True(completed.CompletedAt.HasValue);

            Assert.Equal(3, db.RenewalTaskHistories.Count(x =>
                x.RenewalTaskId == MockFamilfaClientSeeder.Ids.TaskSubmitted ||
                x.RenewalTaskId == MockFamilfaClientSeeder.Ids.TaskAssigned ||
                x.RenewalTaskId == MockFamilfaClientSeeder.Ids.TaskCompleted));
        });
    }

    // ==================================================================
    // 6. Service request
    // ==================================================================

    [Fact(DisplayName =
        "G6: Submitted early-renewal service request for the expiring passport")]
    public async Task ServiceRequest_Submitted_EarlyRenewal()
    {
        await WithScopeDbContextAsync(db =>
        {
            var request = db.ServiceRequests.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.ServiceRequest);
            Assert.Equal(CompanyG, request.ClientCompanyId);
            Assert.Equal(ServiceRequestType.EarlyRenewal, request.Type);
            Assert.Equal(MockFamilfaClientSeeder.Ids.EmployeeHassan,
                request.EmployeeId);
            Assert.Equal(ServiceRequestStatus.Submitted, request.Status);
            Assert.False(string.IsNullOrWhiteSpace(request.Description));
        });
    }

    // ==================================================================
    // 7. Notifications
    // ==================================================================

    [Fact(DisplayName =
        "G7: Non-sensitive mock notifications target the familfa client")]
    public async Task Notifications_ForFamilfaClient()
    {
        await WithScopeDbContextAsync(db =>
        {
            var notifications = db.Notifications
                .Where(x =>
                    x.UserId == ClientUserId &&
                    x.Id >= MockFamilfaClientSeeder.Ids.NotificationWelcome &&
                    x.Id <= MockFamilfaClientSeeder.Ids.NotificationExpiring)
                .ToList();

            Assert.Equal(2, notifications.Count);
            Assert.All(notifications, n => Assert.False(n.IsRead));
            Assert.All(notifications, n =>
                Assert.False(string.IsNullOrWhiteSpace(n.Title)));
        });
    }

    // ==================================================================
    // 8-9. Billing rows / no payment orders
    // ==================================================================

    [Fact(DisplayName =
        "G8: Pending retainer + service-fee invoices and an invoiced gov fee")]
    public async Task Billing_Pending_Linked()
    {
        await WithScopeDbContextAsync(db =>
        {
            var retainer = db.RetainerInvoices.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.RetainerInvoice);
            Assert.Equal(MockFamilfaClientSeeder.RetainerInvoiceNumber, retainer.InvoiceNumber);
            Assert.Equal(CompanyG, retainer.ClientCompanyId);
            Assert.Equal(MockFamilfaClientSeeder.Ids.Contract, retainer.ServiceContractId);
            Assert.Equal(RetainerInvoiceStatus.Pending, retainer.Status);
            Assert.Equal(1000m, retainer.Amount);
            Assert.Equal("AED", retainer.Currency);
            Assert.Null(retainer.PaidAt);

            var serviceFee = db.ServiceFeeInvoices.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.ServiceFeeInvoice);
            Assert.Equal(MockFamilfaClientSeeder.ServiceFeeInvoiceNumber, serviceFee.InvoiceNumber);
            Assert.Equal(CompanyG, serviceFee.ClientCompanyId);
            Assert.Equal(MockFamilfaClientSeeder.Ids.TaskCompleted,
                serviceFee.RenewalTaskId);
            Assert.Equal(ServiceFeeInvoiceStatus.Pending, serviceFee.Status);
            Assert.Equal(750m, serviceFee.Amount);
            Assert.Equal("AED", serviceFee.Currency);
            Assert.Null(serviceFee.PaidAt);

            var govFee = db.GovFeeDisbursements.Single(x =>
                x.Id == MockFamilfaClientSeeder.Ids.GovFeeDisbursement);
            Assert.Equal(CompanyG, govFee.ClientCompanyId);
            Assert.Equal(MockFamilfaClientSeeder.Ids.TaskAssigned,
                govFee.RenewalTaskId);
            Assert.Equal(MockFamilfaClientSeeder.GovernmentReference,
                govFee.GovernmentReference);
            Assert.Equal(GovFeeDisbursementStatus.InvoicedToClient, govFee.Status);
            Assert.NotEqual(default(DateTime), govFee.PaidByFirmAt);
            Assert.True(govFee.InvoicedAt.HasValue);
            Assert.False(govFee.ReimbursedAt.HasValue);
        });
    }

    [Fact(DisplayName =
        "G9: Seeder never creates payment orders and never marks invoices paid")]
    public async Task No_PaymentOrder_And_NoPaid_Invoices()
    {
        await WithScopeDbContextAsync(db =>
        {
            Assert.DoesNotContain(db.PaymentOrders.IgnoreQueryFilters(),
                x => x.ClientCompanyId == CompanyG);

            Assert.All(db.RetainerInvoices.IgnoreQueryFilters()
                .Where(x => x.ClientCompanyId == CompanyG),
                invoice => Assert.Equal(
                    RetainerInvoiceStatus.Pending, invoice.Status));
            Assert.All(db.ServiceFeeInvoices.IgnoreQueryFilters()
                .Where(x => x.ClientCompanyId == CompanyG),
                invoice => Assert.Equal(
                    ServiceFeeInvoiceStatus.Pending, invoice.Status));
        });
    }

    // ==================================================================
    // 10. Idempotency
    // ==================================================================

    [Fact(DisplayName =
        "G10: Re-running the seeder inserts nothing and stays consistent")]
    public async Task Seeder_IsIdempotent()
    {
        await WithScopeDbContextAsync(db =>
        {
            var before = CountMockRows(db);

            MockFamilfaClientSeeder.Seed(db);

            var after = CountMockRows(db);
            Assert.Equal(before, after);
        });
    }

    private static (int Users, int Documents, int Tasks, int RetInvoices, int SfInvoices, int GovFees) CountMockRows(
        ApplicationDbContext db)
        => (
            db.Users.IgnoreQueryFilters().Count(x =>
                x.Email == MockFamilfaClientSeeder.ClientEmail),
            db.Documents.IgnoreQueryFilters().Count(x =>
                x.DocumentNumber.StartsWith("TEST-")),
            db.RenewalTasks.IgnoreQueryFilters().Count(x =>
                MockFamilfaClientSeeder.Ids.TaskSubmitted == x.Id ||
                MockFamilfaClientSeeder.Ids.TaskAssigned == x.Id ||
                MockFamilfaClientSeeder.Ids.TaskCompleted == x.Id),
            db.RetainerInvoices.IgnoreQueryFilters().Count(x =>
                x.InvoiceNumber == MockFamilfaClientSeeder.RetainerInvoiceNumber),
            db.ServiceFeeInvoices.IgnoreQueryFilters().Count(x =>
                x.InvoiceNumber == MockFamilfaClientSeeder.ServiceFeeInvoiceNumber),
            db.GovFeeDisbursements.IgnoreQueryFilters().Count(x =>
                x.GovernmentReference == MockFamilfaClientSeeder.GovernmentReference));
}