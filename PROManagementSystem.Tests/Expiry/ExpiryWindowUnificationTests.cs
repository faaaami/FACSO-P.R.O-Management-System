using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;
using PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// Locks the shared 90-day expiry window on the read paths. The renewal
/// job, the document read repositories and the employee read repository
/// must agree: a document inside the window reports ExpiringSoon rather
/// than Active, and a document beyond the window is not returned by a
/// 90-day query.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExpiryWindowUnificationTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@test.local";

    public ExpiryWindowUnificationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

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

    private Task SetDocumentAExpiryAsync(
        int daysFromNow)
        => WithScopeDbContextAsync(db =>
        {
            var document = db.Documents.Single(
                x => x.Id == TestDataSeeder.Ids.DocumentA);

            document.ExpiryDate =
                DateTime.UtcNow.AddDays(daysFromNow);

            db.SaveChanges();
        });

    private async Task<
        GetExpiringEmployeesResponseDto>
        GetExpiringEmployeesAsync(
            string adminToken,
            int days)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1/admin/employees/expiring" +
                $"?days={days}&page=1&pageSize=50");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response =
            await AuthClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetExpiringEmployeesResponseDto>>(
            response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    private async Task<GetEmployeeDocumentsResponseDto>
        GetEmployeeDocumentsAsync(
            string adminToken,
            Guid employeeId)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/admin/employees/{employeeId}/documents");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response =
            await AuthClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetEmployeeDocumentsResponseDto>>(
            response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    [Fact(DisplayName =
        "A document 60 days out is inside the 90-day window and is " +
        "labelled ExpiringSoon, not Active")]
    public async Task DocumentAt60Days_IsLabelledExpiringSoon()
    {
        var adminToken = await LoginAsAdminAsync();

        await SetDocumentAExpiryAsync(60);

        var result = await GetExpiringEmployeesAsync(
            adminToken,
            days: 90);

        var item = Assert.Single(
            result.Items,
            x => x.DocumentId ==
                TestDataSeeder.Ids.DocumentA);

        Assert.Equal("ExpiringSoon", item.Status);
    }

    [Fact(DisplayName =
        "A document 89 days out is still inside the 90-day window")]
    public async Task DocumentAt89Days_IsLabelledExpiringSoon()
    {
        var adminToken = await LoginAsAdminAsync();

        await SetDocumentAExpiryAsync(89);

        var result = await GetExpiringEmployeesAsync(
            adminToken,
            days: 90);

        var item = Assert.Single(
            result.Items,
            x => x.DocumentId ==
                TestDataSeeder.Ids.DocumentA);

        Assert.Equal("ExpiringSoon", item.Status);
    }

    [Fact(DisplayName =
        "A document 91 days out falls outside a 90-day query")]
    public async Task DocumentAt91Days_IsOutsideThe90DayWindow()
    {
        var adminToken = await LoginAsAdminAsync();

        await SetDocumentAExpiryAsync(91);

        var result = await GetExpiringEmployeesAsync(
            adminToken,
            days: 90);

        Assert.DoesNotContain(
            result.Items,
            x => x.DocumentId ==
                TestDataSeeder.Ids.DocumentA);
    }

    [Fact(DisplayName =
        "Employee document list reports ExpiringSoon for a document " +
        "inside the shared 90-day window")]
    public async Task EmployeeDocumentList_UsesTheShared90DayWindow()
    {
        var adminToken = await LoginAsAdminAsync();

        await SetDocumentAExpiryAsync(60);

        var result = await GetEmployeeDocumentsAsync(
            adminToken,
            TestDataSeeder.Ids.EmployeeA);

        var item = Assert.Single(
            result.Items,
            x => x.Id == TestDataSeeder.Ids.DocumentA);

        Assert.Equal(
            DocumentStatus.ExpiringSoon,
            item.Status);
    }
}
