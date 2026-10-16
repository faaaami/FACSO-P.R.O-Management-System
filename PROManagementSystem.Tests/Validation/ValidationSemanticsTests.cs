using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Verifies the Day 6 H2 fix: invalid input on the C1/C2-gated surface is
/// rejected with a 400 (VALIDATION_ERROR) instead of leaking through to the
/// handler as a 409 conflict, a 500 (NullReferenceException / SQL error) or a
/// silently accepted value. All requests use the real JWT pipeline.
/// Locked 409/404 semantics (status-machine transitions, ownership, scope) are
/// untouched and asserted by their own suites.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ValidationSemanticsTests : IntegrationTestBase
{
    private static readonly byte[] PdfBytes =
    [
        0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
        0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
    ];

    public ValidationSemanticsTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string AgentToken(Guid userId)
        => JwtHelper.MintToken(
            userId,
            "Agent",
            userId == TestDataSeeder.Ids.AgentB
                ? "agent-b@test.local"
                : "agent-a@test.local",
            userId == TestDataSeeder.Ids.AgentB ? "Agent B" : "Agent A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

    private static string AdminToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.Admin,
            "Admin",
            "admin@test.local",
            "System Admin");

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? body,
        string token)
    {
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return await AuthClient.SendAsync(request);
    }

    private static async Task AssertValidation400Async(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
    }

    /// <summary>
    /// For DTOs with non-nullable required members (DocumentNumber, FileUrl)
    /// the 400 can also be produced upfront by ASP.NET Core's automatic
    /// model-state validation (RFC 9110 problem-details body) before the
    /// command ever reaches the pipeline. Either body is acceptable: the
    /// guarantee asserted here is 400 and never 500.
    /// </summary>
    private static async Task Assert400Not500Async(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("INTERNAL_SERVER_ERROR", body);
    }

    private static async Task AssertBadRequestAsync(
        HttpResponseMessage response,
        string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(code, body);
    }

    private async Task<HttpResponseMessage> ExtractAsync()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/agent/documents/extract");
        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", AgentToken(TestDataSeeder.Ids.AgentA));

        using var file = new ByteArrayContent(
            PdfBytes);
        file.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", "document.pdf");
        form.Add(new StringContent("Passport"), "documentType");
        request.Content = form;

        return await AuthClient.SendAsync(request);
    }

    private async Task<Guid> ExtractIdAsync()
    {
        var response = await ExtractAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope =
            await ReadJsonAsync<ExtractEnvelope>(response);
        Assert.NotNull(envelope?.Data?.ExtractionId);
        return envelope!.Data!.ExtractionId;
    }

    // ------------------------------------------------------------------
    // Agent task status: invalid enum value -> 400 (was 409)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Status with an unrecognized string value is a 400 (was 409)")]
    public async Task A_InvalidStatusString_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Patch,
            $"/api/v1/agent/tasks/{TestDataSeeder.Ids.TaskA}/status",
            new { status = "NotAStatus", note = (string?)null },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "Status with a numeric value outside the enum is a 400 (was 409)")]
    public async Task B_NumericUndefinedStatus_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Patch,
            $"/api/v1/agent/tasks/{TestDataSeeder.Ids.TaskA}/status",
            new { status = "99", note = (string?)null },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Confirm extraction: missing/invalid fields -> 400 (was 500 / 409)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Confirming an extraction without a documentNumber is a 400 (was 500)")]
    public async Task C_ConfirmWithoutDocumentNumber_Returns400()
    {
        var extractionId = await ExtractIdAsync();

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/documents/extractions/{extractionId}/confirm",
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = DocumentType.Passport
            },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await Assert400Not500Async(response);
    }

    [Fact(DisplayName =
        "Confirming an extraction with no owner is a 400 (was 409)")]
    public async Task D_ConfirmWithoutOwner_Returns400()
    {
        var extractionId = await ExtractIdAsync();

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/documents/extractions/{extractionId}/confirm",
            new
            {
                employeeId = (Guid?)null,
                clientEntityId = (Guid?)null,
                type = DocumentType.Passport,
                documentNumber = "DOC-X"
            },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "Confirming an extraction with expiry before issue is a 400 (was 409)")]
    public async Task E_ConfirmExpiryBeforeIssue_Returns400()
    {
        var extractionId = await ExtractIdAsync();

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/documents/extractions/{extractionId}/confirm",
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = DocumentType.Passport,
                documentNumber = "DOC-X",
                issueDate = DateTime.UtcNow,
                expiryDate = DateTime.UtcNow.AddDays(-1)
            },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Register document: missing required fields -> 400 (was 500)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Registering an employee document without a documentNumber is a 400 (was 500)")]
    public async Task F_RegisterEmployeeDocumentWithoutNumber_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/employees/{TestDataSeeder.Ids.EmployeeA}/documents",
            new { type = DocumentType.Passport },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await Assert400Not500Async(response);
    }

    [Fact(DisplayName =
        "Registering an entity document without a documentNumber is a 400 (was 500)")]
    public async Task G_RegisterEntityDocumentWithoutNumber_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/entities/{TestDataSeeder.Ids.EntityA}/documents",
            new { type = DocumentType.Passport },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await Assert400Not500Async(response);
    }

    [Fact(DisplayName =
        "Creating a document version without a fileUrl is a 400 (was 500)")]
    public async Task H_CreateVersionWithoutFileUrl_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}/versions",
            new { },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await Assert400Not500Async(response);
    }

    // ------------------------------------------------------------------
    // Patch document: cross-field date ordering -> 400 (was 409)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Patching expiry before issue is a 400 (was 409)")]
    public async Task I_PatchDocumentExpiryBeforeIssue_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Patch,
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new
            {
                issueDate = DateTime.UtcNow,
                expiryDate = DateTime.UtcNow.AddDays(-1)
            },
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Agent list queries: pagination bounds -> 400 (was 500)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "GET agent documents with page=0 is a 400 (was 500)")]
    public async Task J_GetDocumentsPageZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/agent/documents?page=0",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET agent expiring documents with days=0 is a 400 (was 500)")]
    public async Task K_GetExpiringDaysZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/agent/documents/expiring?days=0",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET agent expiring documents with days beyond 365 is a 400 (was 500)")]
    public async Task L_GetExpiringDaysOutOfRange_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/agent/documents/expiring?days=400",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET agent employees with pageSize beyond 100 is a 400 (was 500)")]
    public async Task M_GetEmployeesPageSizeTooLarge_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/agent/employees?pageSize=1001",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET agent clients with page=0 is a 400 (was 500)")]
    public async Task N_GetClientsPageZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/agent/clients?page=0",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET agent client entities with pageSize=0 is a 400 (was 500)")]
    public async Task O_GetClientEntitiesPageSizeZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/agent/clients/{TestDataSeeder.Ids.CompanyA}/entities?pageSize=0",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "In-bounds pagination still succeeds (regression)")]
    public async Task P_InBoundsPagination_Returns200()
    {
        var documents = await SendAsync(
            HttpMethod.Get,
            "/api/v1/agent/documents?page=1&pageSize=20",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));
        Assert.Equal(HttpStatusCode.OK, documents.StatusCode);

        var expiring = await SendAsync(
            HttpMethod.Get,
            "/api/v1/agent/documents/expiring?days=30",
            null,
            AgentToken(TestDataSeeder.Ids.AgentA));
        Assert.Equal(HttpStatusCode.OK, expiring.StatusCode);
    }

    // ------------------------------------------------------------------
    // Admin list queries (C2 modules): pagination -> 400 (was 500)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "GET retainer-invoices with page=0 is a 400 (was 500)")]
    public async Task Q_RetainerInvoicesPageZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/retainer-invoices?page=0",
            null,
            AdminToken());

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET payment-orders with pageSize beyond 100 is a 400 (was 500)")]
    public async Task R_PaymentOrdersPageSizeTooLarge_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/payment-orders?pageSize=1001",
            null,
            AdminToken());

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET gov-fee-disbursements with page=0 is a 400 (was 500)")]
    public async Task S_GovFeeDisbursementsPageZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/gov-fee-disbursements?page=0",
            null,
            AdminToken());

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET service-fee-invoices with pageSize=0 is a 400 (was 500)")]
    public async Task T_ServiceFeeInvoicesPageSizeZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/service-fee-invoices?pageSize=0",
            null,
            AdminToken());

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "GET service-requests with page=0 is a 400 (was 500)")]
    public async Task U_ServiceRequestsPageZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/service-requests?page=0",
            null,
            AdminToken());

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Dashboard (controller-level guards, no MediatR pipeline)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "GET dashboard activity-feed with page=0 is a 400 (was 500)")]
    public async Task V_DashboardActivityFeedPageZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/dashboard/activity-feed?page=0",
            null,
            AdminToken());

        await AssertBadRequestAsync(response, "INVALID_PAGE");
    }

    [Fact(DisplayName =
        "GET dashboard expiring documents with days=0 is a 400")]
    public async Task W_DashboardExpiringDocsDaysZero_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/dashboard/expiry-alerts/documents?days=0",
            null,
            AdminToken());

        await AssertBadRequestAsync(response, "INVALID_DAYS");
    }

    // ------------------------------------------------------------------
    // Envelope for the extract response
    // ------------------------------------------------------------------

    private sealed class ExtractEnvelope
    {
        public bool Success { get; set; }

        public ExtractPayload? Data { get; set; }
    }

    private sealed class ExtractPayload
    {
        public Guid ExtractionId { get; set; }
    }
}