using System.Net;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoicePdf;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoicePdf;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// Week 3 Day 4 - Stage 1: the billing read layer accepts an optional
/// clientCompanyId scope. Admin callers omit it (cross-company reads keep
/// working); a supplied scope MUST filter to that company, and a row that
/// belongs to a different company surfaces as 404 (not 403). Also locks in
/// the gov-fee status-filter type fix and string-enum responses.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class BillingReadTenancyTests : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;
    private static readonly Guid CompanyB = TestDataSeeder.Ids.CompanyB;
    private static readonly Guid Unknown = TestDataSeeder.Ids.Unknown;

    public BillingReadTenancyTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    /// <summary>
    /// Sends a request through the real MediatR pipeline (including the
    /// FluentValidation behaviour) against the test database. Used for the
    /// client-scoped reads because Stage 1 ships no client-facing HTTP
    /// endpoints yet - the scope is passed in explicitly, mirroring how a
    /// client controller would resolve it from the token later.
    /// </summary>
    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider
            .GetRequiredService<ISender>();
        return await sender.Send(request, CancellationToken.None);
    }

    private async Task<JsonArray?> GetListItemsAsync(
        string url, Guid userId, string role)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, userId, role, clientCompanyId: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        return body["data"]!["items"]!.AsArray();
    }

    // ==================================================================
    // Retainer invoices - admin (unscoped) behavior preserved
    // ==================================================================

    [Fact(DisplayName =
        "R1: Admin reads any company's retainer invoice by ID (200, unscoped)")]
    public async Task RetainerInvoice_Admin_CanReadAnyCompany()
    {
        var invoiceA = await SendAsync(
            new GetRetainerInvoiceByIdQuery(
                TestDataSeeder.Ids.RetainerInvoiceA));
        Assert.Equal(CompanyA, invoiceA.ClientCompanyId);
        Assert.Equal("INV-R-A-001", invoiceA.InvoiceNumber);

        var invoiceB = await SendAsync(
            new GetRetainerInvoiceByIdQuery(
                TestDataSeeder.Ids.RetainerInvoiceB));
        Assert.Equal(CompanyB, invoiceB.ClientCompanyId);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}",
                Admin, role: "Admin", clientCompanyId: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ==================================================================
    // Retainer invoices - client-scoped reads
    // ==================================================================

    [Fact(DisplayName =
        "R2: Client-scoped read of its own retainer invoice succeeds")]
    public async Task RetainerInvoice_OwnCompany_ReturnsInvoice()
    {
        var invoice = await SendAsync(
            new GetRetainerInvoiceByIdQuery(
                TestDataSeeder.Ids.RetainerInvoiceA, CompanyA));

        Assert.Equal(TestDataSeeder.Ids.RetainerInvoiceA, invoice.Id);
        Assert.Equal(CompanyA, invoice.ClientCompanyId);
        Assert.Equal(RetainerInvoiceStatus.Pending, invoice.Status);
    }

    [Fact(DisplayName =
        "R3: Client-scoped read of another company's retainer invoice is 404")]
    public async Task RetainerInvoice_CrossCompany_ReturnsNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new GetRetainerInvoiceByIdQuery(
                TestDataSeeder.Ids.RetainerInvoiceB, CompanyA)));

        Assert.Contains("not found", exception.Message);
    }

    [Fact(DisplayName =
        "R4: Client-scoped read of an unknown retainer invoice is 404")]
    public async Task RetainerInvoice_Unknown_ReturnsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new GetRetainerInvoiceByIdQuery(Unknown, CompanyA)));
    }

    // ==================================================================
    // Retainer invoice PDF
    // ==================================================================

    [Fact(DisplayName =
        "R5: Client-scoped retainer invoice PDF generation succeeds")]
    public async Task RetainerInvoicePdf_OwnCompany_ReturnsPdf()
    {
        var pdf = await SendAsync(
            new GetRetainerInvoicePdfQuery(
                TestDataSeeder.Ids.RetainerInvoiceA, CompanyA));

        Assert.Equal(
            "retainer-invoice-INV-R-A-001.pdf",
            pdf.FileName);
        Assert.NotEmpty(pdf.PdfBytes);
        Assert.True(pdf.PdfBytes.Length > 1000);
    }

    [Fact(DisplayName =
        "R6: Client-scoped retainer invoice PDF for another company is 404")]
    public async Task RetainerInvoicePdf_CrossCompany_ReturnsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new GetRetainerInvoicePdfQuery(
                TestDataSeeder.Ids.RetainerInvoiceB, CompanyA)));
    }

    // ==================================================================
    // Service-fee invoices - client-scoped reads + admin behavior
    // ==================================================================

    [Fact(DisplayName =
        "SF1: Admin reads any company's service-fee invoice by ID (unscoped)")]
    public async Task ServiceFeeInvoice_Admin_CanReadAnyCompany()
    {
        var invoiceA = await SendAsync(
            new GetServiceFeeInvoiceByIdQuery(
                TestDataSeeder.Ids.ServiceFeeInvoiceA));
        Assert.Equal(CompanyA, invoiceA.ClientCompanyId);
        Assert.Equal("INV-SF-A-001", invoiceA.InvoiceNumber);

        var invoiceB = await SendAsync(
            new GetServiceFeeInvoiceByIdQuery(
                TestDataSeeder.Ids.ServiceFeeInvoiceB));
        Assert.Equal(CompanyB, invoiceB.ClientCompanyId);
    }

    [Fact(DisplayName =
        "SF2: Client-scoped read of its own service-fee invoice succeeds")]
    public async Task ServiceFeeInvoice_OwnCompany_ReturnsInvoice()
    {
        var invoice = await SendAsync(
            new GetServiceFeeInvoiceByIdQuery(
                TestDataSeeder.Ids.ServiceFeeInvoiceA, CompanyA));

        Assert.Equal(TestDataSeeder.Ids.ServiceFeeInvoiceA, invoice.Id);
        Assert.Equal(CompanyA, invoice.ClientCompanyId);
        Assert.Equal(ServiceFeeInvoiceStatus.Pending, invoice.Status);
    }

    [Fact(DisplayName =
        "SF3: Client-scoped read of another company's service-fee invoice is 404")]
    public async Task ServiceFeeInvoice_CrossCompany_ReturnsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new GetServiceFeeInvoiceByIdQuery(
                TestDataSeeder.Ids.ServiceFeeInvoiceB, CompanyA)));
    }

    [Fact(DisplayName =
        "SF4: Client-scoped service-fee invoice PDF generation succeeds")]
    public async Task ServiceFeeInvoicePdf_OwnCompany_ReturnsPdf()
    {
        var pdf = await SendAsync(
            new GetServiceFeeInvoicePdfQuery(
                TestDataSeeder.Ids.ServiceFeeInvoiceA, CompanyA));

        Assert.Equal(
            "service-fee-invoice-INV-SF-A-001.pdf",
            pdf.FileName);
        Assert.True(pdf.PdfBytes.Length > 1000);
    }

    [Fact(DisplayName =
        "SF5: Client-scoped service-fee invoice PDF for another company is 404")]
    public async Task ServiceFeeInvoicePdf_CrossCompany_ReturnsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new GetServiceFeeInvoicePdfQuery(
                TestDataSeeder.Ids.ServiceFeeInvoiceB, CompanyA)));
    }

    // ==================================================================
    // Gov-fee disbursements - tenant-scoped reads + status filter fix
    // ==================================================================

    [Fact(DisplayName =
        "G1: Admin reads any company's gov-fee disbursement by ID (unscoped)")]
    public async Task GovFeeDisbursement_Admin_CanReadAnyCompany()
    {
        var itemA = await SendAsync(
            new GetGovFeeDisbursementByIdQuery(
                TestDataSeeder.Ids.GovFeeDisbursementA));
        Assert.Equal(CompanyA, itemA.ClientCompanyId);
        Assert.Equal(GovFeeDisbursementStatus.PaidByFirm, itemA.Status);

        var itemB = await SendAsync(
            new GetGovFeeDisbursementByIdQuery(
                TestDataSeeder.Ids.GovFeeDisbursementB));
        Assert.Equal(CompanyB, itemB.ClientCompanyId);
    }

    [Fact(DisplayName =
        "G2: Client-scoped gov-fee disbursement by ID succeeds for its own company")]
    public async Task GovFeeDisbursement_OwnCompany_ReturnsItem()
    {
        var item = await SendAsync(
            new GetGovFeeDisbursementByIdQuery(
                TestDataSeeder.Ids.GovFeeDisbursementC, CompanyA));

        Assert.Equal(TestDataSeeder.Ids.GovFeeDisbursementC, item.Id);
        Assert.Equal(GovFeeDisbursementStatus.Reimbursed, item.Status);
    }

    [Fact(DisplayName =
        "G3: Client-scoped gov-fee disbursement for another company is 404")]
    public async Task GovFeeDisbursement_CrossCompany_ReturnsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new GetGovFeeDisbursementByIdQuery(
                TestDataSeeder.Ids.GovFeeDisbursementB, CompanyA)));
    }

    [Fact(DisplayName =
        "G4: Client-scoped gov-fee list shows only its own company's rows")]
    public async Task GovFeeList_Scoped_ReturnsOnlyOwnCompany()
    {
        var companyA = await SendAsync(
            new GetGovFeeDisbursementsQuery(CompanyA, null, 1, 50));
        var companyAIds = companyA.Items.Select(x => x.Id).ToList();

        Assert.Contains(TestDataSeeder.Ids.GovFeeDisbursementA, companyAIds);
        Assert.Contains(TestDataSeeder.Ids.GovFeeDisbursementC, companyAIds);
        Assert.DoesNotContain(TestDataSeeder.Ids.GovFeeDisbursementB, companyAIds);
        Assert.All(companyA.Items, x => Assert.Equal(CompanyA, x.ClientCompanyId));

        var companyB = await SendAsync(
            new GetGovFeeDisbursementsQuery(CompanyB, null, 1, 50));
        var companyBIds = companyB.Items.Select(x => x.Id).ToList();

        Assert.Contains(TestDataSeeder.Ids.GovFeeDisbursementB, companyBIds);
        Assert.DoesNotContain(TestDataSeeder.Ids.GovFeeDisbursementA, companyBIds);
        Assert.DoesNotContain(TestDataSeeder.Ids.GovFeeDisbursementC, companyBIds);
    }

    [Theory(DisplayName =
        "G5: Gov-fee status filter returns exactly the rows in that status")]
    [InlineData(GovFeeDisbursementStatus.PaidByFirm)]
    [InlineData(GovFeeDisbursementStatus.InvoicedToClient)]
    [InlineData(GovFeeDisbursementStatus.Reimbursed)]
    public async Task GovFeeList_StatusFilter_ReturnsOnlyMatchingStatus(
        GovFeeDisbursementStatus status)
    {
        var response = await SendAsync(
            new GetGovFeeDisbursementsQuery(CompanyA, status, 1, 50));

        Assert.All(
            response.Items,
            x => Assert.Equal(status, x.Status));
    }

    [Fact(DisplayName =
        "G6a: Admin gov-fee list PaidByFirm status returns only CompanyA-PaidByFirm")]
    public async Task GovFeeList_ViaHttp_PaidByFirmFilter()
    {
        var items = await GetListItemsAsync(
            $"/api/v1/gov-fee-disbursements?clientCompanyId={CompanyA}&status=PaidByFirm",
            Admin,
            "Admin");

        var item = Assert.Single(items!);
        Assert.Equal(TestDataSeeder.Ids.GovFeeDisbursementA,
            Guid.Parse((string)item!["id"]!));
        Assert.Equal("PaidByFirm", (string)item["status"]!);
    }

    [Fact(DisplayName =
        "G6b: Admin gov-fee list InvoicedToClient status returns only CompanyB-InvoicedToClient")]
    public async Task GovFeeList_ViaHttp_InvoicedToClientFilter()
    {
        var items = await GetListItemsAsync(
            $"/api/v1/gov-fee-disbursements?clientCompanyId={CompanyB}&status=InvoicedToClient",
            Admin,
            "Admin");

        var item = Assert.Single(items!);
        Assert.Equal(TestDataSeeder.Ids.GovFeeDisbursementB,
            Guid.Parse((string)item!["id"]!));
        Assert.Equal("InvoicedToClient", (string)item["status"]!);
    }

    [Fact(DisplayName =
        "G6c: Admin gov-fee list Reimbursed status returns only CompanyA-Reimbursed")]
    public async Task GovFeeList_ViaHttp_ReimbursedFilter()
    {
        var items = await GetListItemsAsync(
            $"/api/v1/gov-fee-disbursements?clientCompanyId={CompanyA}&status=Reimbursed",
            Admin,
            "Admin");

        var item = Assert.Single(items!);
        Assert.Equal(TestDataSeeder.Ids.GovFeeDisbursementC,
            Guid.Parse((string)item!["id"]!));
        Assert.Equal("Reimbursed", (string)item["status"]!);
    }

    [Fact(DisplayName =
        "G7: Admin gov-fee list without a status filter returns all company rows")]
    public async Task GovFeeList_ViaHttp_CompanyScoped()
    {
        var items = await GetListItemsAsync(
            $"/api/v1/gov-fee-disbursements?clientCompanyId={CompanyA}",
            Admin,
            "Admin");

        Assert.Equal(2, items!.Count);

        var ids = items!
            .Select(x => Guid.Parse((string)x!["id"]!))
            .ToList();

        Assert.Contains(TestDataSeeder.Ids.GovFeeDisbursementA, ids);
        Assert.Contains(TestDataSeeder.Ids.GovFeeDisbursementC, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.GovFeeDisbursementB, ids);
    }
}