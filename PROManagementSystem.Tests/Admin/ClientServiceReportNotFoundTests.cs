using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Admin;

/// <summary>
/// GET /api/v1/admin/clients/{id}/service-report (JSON and PDF) must not
/// fabricate a report for a company that does not exist.
//////
/// The read repository used to return HTTP 200 with the literal company name
/// "Unknown", empty collections and a zeroed summary whenever the client row was
/// missing — which also meant a missing id was excluded by `is_deleted = false`
/// with no way to tell absent from soft-deleted. The PDF path then rendered a
/// real document titled "Service Report — Unknown".
///
/// These tests pin the 404 contract and prove the success path is untouched.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientServiceReportNotFoundTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@test.local";

    private const string JsonRoute = "service-report";

    private const string PdfRoute = "service-report/pdf";

    public ClientServiceReportNotFoundTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Scaffolding
    // ------------------------------------------------------------------

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = AdminEmail,
                password =
                    TestDataSeeder.TestLoginPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<LoginResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data.AccessToken;
    }

    private static HttpRequestMessage Get(
        string url,
        string token)
    {
        var request =
            new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private Task<HttpResponseMessage> GetReportAsync(
        string token,
        Guid companyId,
        string route)
        => AuthClient.SendAsync(Get(
            $"/api/v1/admin/clients/{companyId}/{route}",
            token));

    private async Task SoftDeleteCompanyAsync(Guid companyId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            var company = await db.ClientCompanies
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == companyId);

            company.IsDeleted = true;
            company.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // Success path must not regress
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "An existing active company still returns a populated report")]
    public async Task ExistingCompany_StillReturnsReport()
    {
        var token = await LoginAsAdminAsync();

        var response = await GetReportAsync(
            token,
            TestDataSeeder.Ids.CompanyA,
            JsonRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<ClientServiceReportDto>>(response);

        Assert.NotNull(envelope?.Data);

        Assert.Equal(
            TestDataSeeder.Ids.CompanyA,
            envelope!.Data.Client.Id);

        Assert.NotEqual(
            "Unknown",
            envelope.Data.Client.CompanyName);

        Assert.NotEmpty(envelope.Data.Tasks);
        Assert.NotEmpty(envelope.Data.Invoices);
    }

    [Fact(DisplayName =
        "An existing active company still returns a PDF")]
    public async Task ExistingCompany_StillReturnsPdf()
    {
        var token = await LoginAsAdminAsync();

        var response = await GetReportAsync(
            token,
            TestDataSeeder.Ids.CompanyA,
            PdfRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.NotEmpty(bytes);

        // PDF magic number: still a real document, not an error payload.
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    // ------------------------------------------------------------------
    // The defect
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "JSON report for a company that does not exist returns 404")]
    public async Task Json_MissingCompany_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var response = await GetReportAsync(
            token,
            Guid.NewGuid(),
            JsonRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Unknown", body);
    }

    [Fact(DisplayName =
        "JSON report for a soft-deleted company returns 404")]
    public async Task Json_SoftDeletedCompany_Returns404()
    {
        var token = await LoginAsAdminAsync();

        await SoftDeleteCompanyAsync(
            TestDataSeeder.Ids.CompanyA);

        var response = await GetReportAsync(
            token,
            TestDataSeeder.Ids.CompanyA,
            JsonRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "PDF report for a company that does not exist returns 404")]
    public async Task Pdf_MissingCompany_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var response = await GetReportAsync(
            token,
            Guid.NewGuid(),
            PdfRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Must NOT be a rendered "Service Report — Unknown" document.
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.NotEqual((byte)'%', bytes.Length > 0 ? bytes[0] : (byte)0);
    }

    [Fact(DisplayName =
        "PDF report for a soft-deleted company returns 404")]
    public async Task Pdf_SoftDeletedCompany_Returns404()
    {
        var token = await LoginAsAdminAsync();

        await SoftDeleteCompanyAsync(
            TestDataSeeder.Ids.CompanyA);

        var response = await GetReportAsync(
            token,
            TestDataSeeder.Ids.CompanyA,
            PdfRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}