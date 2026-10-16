using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.ApiEnrichment;

/// <summary>
/// Task 7B.3: API enrichment for the jsonb Document.Details column.
///
/// create/register/confirm requests MAY carry an optional JSON-object
/// <c>details</c> payload which is persisted verbatim; omitted/null maps to the
/// empty object. GET-by-id responses expose <c>details</c> as a JSON object for
/// admin/agent/client. Admin update replaces when a non-null object is
/// supplied and preserves otherwise. The agent PATCH accepts <c>details</c> as a
/// whole-object replacement. SuggestedDetails is exposed (currently the empty
/// object) on extract/extraction responses but is NEVER auto-copied into
/// Details on confirmation. List/scan/expiring/dashboard surfaces are
/// unchanged (no details key).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class Task73ApiEnrichmentTests : IntegrationTestBase
{
    private const string DetailsEmpNo = "EMP-1234";
    private const string DetailsFallback = "fallback";

    private static readonly byte[] PdfBytes =
    [
        0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
        0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
    ];

    public Task73ApiEnrichmentTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Request-body helpers
    // ------------------------------------------------------------------

    private static object EntityDocBody(string documentNumber, object? details)
        => new
        {
            type = "Passport",
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            fileUrl = "https://storage.test/dummy",
            fileName = "doc.pdf",
            contentType = "application/pdf",
            fileSize = 2048,
            details
        };

    private static object EmployeeDocBody(string documentNumber, object? details)
        => new
        {
            type = "Passport",
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            fileUrl = "https://storage.test/dummy",
            fileName = "doc.pdf",
            contentType = "application/pdf",
            fileSize = 2048,
            details
        };

    private static object UpdateBody(
        string documentNumber,
        bool isActive,
        object? details)
        => new
        {
            type = "Passport",
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            fileUrl = "https://storage.test/dummy",
            fileName = "doc.pdf",
            contentType = "application/pdf",
            fileSize = 4096,
            isActive,
            details
        };

    private static object UpdateBodyWithoutDetails(
        string documentNumber,
        bool isActive)
        => new Dictionary<string, object?>
        {
            ["type"] = "Passport",
            ["documentNumber"] = documentNumber,
            ["issueDate"] = "2023-01-01T00:00:00Z",
            ["expiryDate"] = "2032-01-01T00:00:00Z",
            ["fileUrl"] = "https://storage.test/dummy",
            ["fileName"] = "doc.pdf",
            ["contentType"] = "application/pdf",
            ["fileSize"] = 4096,
            ["isActive"] = isActive
        };

    private static object ConfirmBody(
        Guid? employeeId,
        Guid? entityId,
        string documentNumber,
        object? details)
        => new
        {
            employeeId,
            clientEntityId = entityId,
            type = "Passport",
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            details
        };

    private static Dictionary<string, object?> Details(
        string documentNumber,
        bool withUnknownKey = false)
    {
        var details = new Dictionary<string, object?>
        {
            // passportNumber satisfies the Passport schema's required field so
            // the shared fixture is valid on every write path, confirmation
            // included (Task 7B.5).
            ["passportNumber"] = "P-1000",
            ["documentNumber"] = documentNumber,
            ["expiryDate"] = "2031-12-31"
        };

        if (withUnknownKey)
        {
            details["vendorSpecific"] = new Dictionary<string, object?>
            {
                ["trackingRef"] = "TRACK-001"
            };
        }

        return details;
    }

    // ------------------------------------------------------------------
    // HTTP helpers
    // ------------------------------------------------------------------

    private Task<HttpResponseMessage> AdminPostEntityDocumentAsync(
        Guid entityId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/admin/entities/{entityId}/documents",
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AdminPostEmployeeDocumentAsync(
        Guid employeeId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/admin/employees/{employeeId}/documents",
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AgentRegisterEntityDocumentAsync(
        Guid entityId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/agent/entities/{entityId}/documents",
            body,
            TestDataSeeder.Ids.AgentA,
            "Agent"));

    private Task<HttpResponseMessage> AgentRegisterEmployeeDocumentAsync(
        Guid employeeId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/agent/employees/{employeeId}/documents",
            body,
            TestDataSeeder.Ids.AgentA,
            "Agent"));

    private Task<HttpResponseMessage> AdminGetAsync(Guid documentId)
        => ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/admin/documents/{documentId}",
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AgentGetAsync(Guid documentId)
        => ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/agent/documents/{documentId}",
            TestDataSeeder.Ids.AgentA,
            "Agent"));

    private Task<HttpResponseMessage> ClientGetAsync(Guid documentId)
        => ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/client/documents/{documentId}",
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

    private async Task<string> ExtractAsync(
        string basePath,
        Guid userId,
        string role)
    {
        using var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", "upload.pdf");
        form.Add(new StringContent("Passport"), "documentType");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/{basePath}/documents/extract")
        {
            Content = form
        };
        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(userId, role));

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    private static Guid ExtractId(string json)
        => Navigate(json, "data").GetProperty("extractionId").GetGuid();

    private Task<HttpResponseMessage> AdminConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/admin/documents/extractions/{extractionId}/confirm",
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AgentConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/agent/documents/extractions/{extractionId}/confirm",
            body,
            TestDataSeeder.Ids.AgentA,
            "Agent"));

    private Task SeedExtractionAsync(
        Guid extractionId,
        Guid? createdBy,
        string suggestedDetails)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(
                new DocumentExtraction
                {
                    Id = extractionId,
                    FileName = "upload.pdf",
                    FileUrl = $"extractions/{extractionId:N}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    SuggestedType = DocumentType.Passport,
                    SuggestedDocumentNumber = "SUG-001",
                    SuggestedDetails = suggestedDetails,
                    Status = "Pending",
                    ExpiresAt = now.AddHours(10),
                    CreatedBy = createdBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

    private Task<string> StoredDetailsAsync(Guid documentId)
    {
        string stored = string.Empty;

        return WithScopeDbContextAsync(db =>
        {
            stored = db.Documents
                .AsNoTracking()
                .Single(x => x.Id == documentId)
                .Details;
        }).ContinueWith(_ => stored, TaskScheduler.Default);
    }

    private async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return Navigate(json, "data").GetProperty("id").GetGuid();
    }

    // ------------------------------------------------------------------
    // JSON assertion helpers
    // ------------------------------------------------------------------

    private static JsonElement Navigate(string json, params string[] path)
    {
        using var document = JsonDocument.Parse(json);

        var element = document.RootElement;
        foreach (var segment in path)
        {
            element = element.GetProperty(segment);
        }

        return element.Clone();
    }

    private static void AssertDetailsEqual(string expectedJson, JsonElement actual)
    {
        using var expected = JsonDocument.Parse(expectedJson);

        Assert.True(
            JsonElement.DeepEquals(expected.RootElement, actual),
            $"Expected details {expectedJson} but got {actual.GetRawText()}");
    }

    private static void AssertKeyAbsent(string json, params string[] absentKeys)
    {
        using var document = JsonDocument.Parse(json);

        foreach (var key in absentKeys)
        {
            Assert.False(
                document.RootElement.TryGetProperty(key, out _),
                $"Response should not expose '{key}'.");
        }
    }

    private static void AssertListItemsHideDetails(string json, int expectedAtLeast)
    {
        using var document = JsonDocument.Parse(json);

        var items = document.RootElement
            .GetProperty("data")
            .GetProperty("items");

        Assert.True(
            items.ValueKind == JsonValueKind.Array &&
            items.GetArrayLength() >= expectedAtLeast);

        foreach (var item in items.EnumerateArray())
        {
            Assert.False(item.TryGetProperty("details", out _));
            Assert.False(item.TryGetProperty("suggestedDetails", out _));
        }
    }

    // ------------------------------------------------------------------
    // Create / register with details
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin creating a client-entity document with details persists and echoes the JSON object")]
    public async Task Admin_CreateEntityDocument_WithDetails_EchoesAndPersists()
    {
        var details = Details(DetailsEmpNo, withUnknownKey: true);

        var response = await AdminPostEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            EntityDocBody("ADM-ENT-001", details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(json, "data", "details"));

        var documentId = await CreatedIdAsync(response);
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            JsonDocument.Parse(await StoredDetailsAsync(documentId)).RootElement);
    }

    [Fact(DisplayName =
        "Admin creating an employee document with details persists and echoes the JSON object")]
    public async Task Admin_CreateEmployeeDocument_WithDetails_EchoesAndPersists()
    {
        var details = Details("ADM-EMP-002");

        var response = await AdminPostEmployeeDocumentAsync(
            TestDataSeeder.Ids.EmployeeA,
            EmployeeDocBody("ADM-EMP-002", details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(json, "data", "details"));

        var documentId = await CreatedIdAsync(response);
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            JsonDocument.Parse(await StoredDetailsAsync(documentId)).RootElement);
    }

    [Fact(DisplayName =
        "Agent registering an entity document with details persists and echoes the JSON object")]
    public async Task Agent_RegisterEntityDocument_WithDetails_EchoesAndPersists()
    {
        var details = Details("AGT-ENT-003");

        var response = await AgentRegisterEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            EntityDocBody("AGT-ENT-003", details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(json, "data", "details"));

        var documentId = await CreatedIdAsync(response);
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            JsonDocument.Parse(await StoredDetailsAsync(documentId)).RootElement);
    }

    [Fact(DisplayName =
        "Agent registering an employee document with details persists and echoes the JSON object")]
    public async Task Agent_RegisterEmployeeDocument_WithDetails_EchoesAndPersists()
    {
        var details = Details("AGT-EMP-004");

        var response = await AgentRegisterEmployeeDocumentAsync(
            TestDataSeeder.Ids.EmployeeA,
            EmployeeDocBody("AGT-EMP-004", details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(json, "data", "details"));

        var documentId = await CreatedIdAsync(response);
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            JsonDocument.Parse(await StoredDetailsAsync(documentId)).RootElement);
    }

    [Fact(DisplayName =
        "Creating a document without a details property stores the empty JSON object")]
    public async Task Create_OmittingDetails_StoresEmptyObject()
    {
        var id = await CreatedIdAsync(
            await AdminPostEntityDocumentAsync(
                TestDataSeeder.Ids.EntityA,
                EntityDocBody("ADM-OMIT-005", null)));

        AssertDetailsEqual(
            "{}",
            JsonDocument.Parse(await StoredDetailsAsync(id)).RootElement);
    }

    [Fact(DisplayName =
        "Creating a document with an explicit null details stores the empty JSON object")]
    public async Task Create_NullDetails_StoresEmptyObject()
    {
        var id = await CreatedIdAsync(
            await AdminPostEntityDocumentAsync(
                TestDataSeeder.Ids.EntityA,
                EntityDocBody("ADM-NULL-006", null)));

        AssertDetailsEqual(
            "{}",
            JsonDocument.Parse(await StoredDetailsAsync(id)).RootElement);
    }

    // ------------------------------------------------------------------
    // Admin update
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin update replacing details with a new JSON object persists it")]
    public async Task Admin_UpdateDocument_WithDetails_Replaces()
    {
        var id = await CreatedIdAsync(
            await AdminPostEntityDocumentAsync(
                TestDataSeeder.Ids.EntityA,
                EntityDocBody("ADM-UPD-007", Details(DetailsEmpNo))));

        var replacement = Details("REPLACED-008");

        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/admin/documents/{id}",
            UpdateBody("ADM-UPD-007", true, replacement),
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertDetailsEqual(
            JsonSerializer.Serialize(replacement),
            Navigate(await response.Content.ReadAsStringAsync(), "data", "details"));

        AssertDetailsEqual(
            JsonSerializer.Serialize(replacement),
            JsonDocument.Parse(await StoredDetailsAsync(id)).RootElement);
    }

    [Fact(DisplayName =
        "Admin update omitting details preserves the stored details")]
    public async Task Admin_UpdateDocument_OmittingDetails_Preserves()
    {
        var original = Details(DetailsEmpNo);

        var id = await CreatedIdAsync(
            await AdminPostEntityDocumentAsync(
                TestDataSeeder.Ids.EntityA,
                EntityDocBody("ADM-PRSV-009", original)));

        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/admin/documents/{id}",
            UpdateBodyWithoutDetails("ADM-PRSV-009", true),
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertDetailsEqual(
            JsonSerializer.Serialize(original),
            JsonDocument.Parse(await StoredDetailsAsync(id)).RootElement);
    }

    [Fact(DisplayName =
        "Admin update with an explicit null details preserves the stored details")]
    public async Task Admin_UpdateDocument_NullDetails_Preserves()
    {
        var original = Details(DetailsEmpNo);

        var id = await CreatedIdAsync(
            await AdminPostEntityDocumentAsync(
                TestDataSeeder.Ids.EntityA,
                EntityDocBody("ADM-NULL-010", original)));

        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/admin/documents/{id}",
            UpdateBody("ADM-NULL-010", true, null),
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertDetailsEqual(
            JsonSerializer.Serialize(original),
            JsonDocument.Parse(await StoredDetailsAsync(id)).RootElement);
    }

    // ------------------------------------------------------------------
    // Agent PATCH
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent PATCH with a details object accepts and replaces the stored details")]
    public async Task Agent_Patch_WithDetails_ReplacesAndEchoes()
    {
        var replacement = Details("AGT-PATCH-011", withUnknownKey: true);

        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { details = replacement },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertDetailsEqual(
            JsonSerializer.Serialize(replacement),
            Navigate(await response.Content.ReadAsStringAsync(), "data", "details"));

        AssertDetailsEqual(
            JsonSerializer.Serialize(replacement),
            JsonDocument.Parse(
                await StoredDetailsAsync(TestDataSeeder.Ids.DocumentA)).RootElement);
    }

    [Fact(DisplayName =
        "Agent PATCH omitting details leaves the stored details unchanged")]
    public async Task Agent_Patch_OmittingDetails_Preserves()
    {
        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { documentNumber = "DOC-A-001" },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The seeded document has no details: the empty object must remain.
        AssertDetailsEqual(
            "{}",
            JsonDocument.Parse(
                await StoredDetailsAsync(TestDataSeeder.Ids.DocumentA)).RootElement);
    }

    [Fact(DisplayName =
        "Agent PATCH with an explicit null details resets the stored details to the empty object")]
    public async Task Agent_Patch_NullDetails_ResetsToEmptyObject()
    {
        await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { details = Details("PRE-FILL") },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { details = (object?)null },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertDetailsEqual(
            "{}",
            JsonDocument.Parse(
                await StoredDetailsAsync(TestDataSeeder.Ids.DocumentA)).RootElement);
    }

    [Fact(DisplayName =
        "Agent PATCH with a non-object details value is rejected with 400")]
    public async Task Agent_Patch_NonObjectDetails_Returns400()
    {
        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { details = "not-an-object" },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent PATCH with an unsupported field remains rejected (whitelist regression)")]
    public async Task Agent_Patch_UnknownField_StillRejected()
    {
        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { type = "Passport" },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Confirmation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin confirming an extraction with details persists and echoes them")]
    public async Task Admin_ConfirmExtraction_WithDetails_StoresAndEchoes()
    {
        var json = await ExtractAsync("admin", TestDataSeeder.Ids.Admin, "Admin");
        var extractionId = ExtractId(json);

        var details = Details("ADM-CNF-015");

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(TestDataSeeder.Ids.EmployeeA, null, "ADM-CNF-015", details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseJson = await response.Content.ReadAsStringAsync();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(responseJson, "data", "details"));

        var documentId = Navigate(responseJson, "data").GetProperty("documentId").GetGuid();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            JsonDocument.Parse(await StoredDetailsAsync(documentId)).RootElement);
    }

    [Fact(DisplayName =
        "Agent confirming an extraction with details persists and echoes them")]
    public async Task Agent_ConfirmExtraction_WithDetails_StoresAndEchoes()
    {
        var json = await ExtractAsync("agent", TestDataSeeder.Ids.AgentA, "Agent");
        var extractionId = ExtractId(json);

        var details = Details("AGT-CNF-016");

        var response = await AgentConfirmAsync(
            extractionId,
            ConfirmBody(TestDataSeeder.Ids.EmployeeA, null, "AGT-CNF-016", details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseJson = await response.Content.ReadAsStringAsync();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(responseJson, "data", "details"));

        var documentId = Navigate(responseJson, "data").GetProperty("documentId").GetGuid();
        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            JsonDocument.Parse(await StoredDetailsAsync(documentId)).RootElement);
    }

    [Fact(DisplayName =
        "Confirmation never auto-copies SuggestedDetails into the confirmed document's Details")]
    public async Task Confirm_NeverCopiesSuggestedDetails()
    {
        var extractionId = Guid.NewGuid();

        await SeedExtractionAsync(
            extractionId,
            createdBy: null,
            suggestedDetails: "{\"suggestedKey\":\"suggested-value\"}");

        var details = Details("ADM-NOCOPY-017");

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(TestDataSeeder.Ids.EmployeeA, null, "ADM-NOCOPY-017", details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var documentId = Navigate(json, "data").GetProperty("documentId").GetGuid();

        var storedDetails =
            JsonDocument.Parse(await StoredDetailsAsync(documentId)).RootElement;

        AssertDetailsEqual(JsonSerializer.Serialize(details), storedDetails);
        Assert.False(storedDetails.TryGetProperty("suggestedKey", out _));
    }

    // ------------------------------------------------------------------
    // Validation negatives
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Creating a document with an array details is rejected with 400")]
    public async Task Create_DetailsArray_Returns400()
    {
        var response = await AdminPostEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            EntityDocBody("ADM-BAD-018", new object[] { 1, 2 }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "Creating a document with a scalar details value is rejected with 400")]
    public async Task Create_DetailsScalar_Returns400()
    {
        var response = await AdminPostEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            EntityDocBody("ADM-BAD-019", 42));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "Confirming an extraction with a string details is rejected with 400")]
    public async Task Confirm_DetailsString_Returns400()
    {
        var extractionJson = await ExtractAsync("agent", TestDataSeeder.Ids.AgentA, "Agent");
        var extractionId = ExtractId(extractionJson);

        var response = await AgentConfirmAsync(
            extractionId,
            ConfirmBody(TestDataSeeder.Ids.EmployeeA, null, "AGT-BAD-020", "oops"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // GET-by-id reads
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin GET-by-id returns the stored details as a JSON object")]
    public async Task GetById_Admin_ReturnsDetailsObject()
    {
        var details = Details(DetailsEmpNo, withUnknownKey: true);

        var id = await CreatedIdAsync(
            await AdminPostEntityDocumentAsync(
                TestDataSeeder.Ids.EntityA,
                EntityDocBody("ADM-GET-021", details)));

        var response = await AdminGetAsync(id);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(await response.Content.ReadAsStringAsync(), "data", "details"));
    }

    [Fact(DisplayName =
        "Agent GET-by-id returns the stored details as a JSON object")]
    public async Task GetById_Agent_ReturnsDetailsObject()
    {
        // Agent GET-by-id is scope-restricted to task-linked documents, so
        // seed details onto the in-scope seeded document via the agent PATCH.
        var details = Details("AGT-GET-022", withUnknownKey: true);

        var patchResponse = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { details },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var getResponse = await AgentGetAsync(TestDataSeeder.Ids.DocumentA);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(await getResponse.Content.ReadAsStringAsync(), "data", "details"));
    }

    [Fact(DisplayName =
        "Client GET-by-id returns the stored details as a JSON object")]
    public async Task GetById_Client_ReturnsDetailsObject()
    {
        var details = Details("CLI-GET-023", withUnknownKey: true);

        var id = await CreatedIdAsync(
            await AdminPostEmployeeDocumentAsync(
                TestDataSeeder.Ids.EmployeeA,
                EmployeeDocBody("CLI-GET-023", details)));

        var response = await ClientGetAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertDetailsEqual(
            JsonSerializer.Serialize(details),
            Navigate(await response.Content.ReadAsStringAsync(), "data", "details"));
    }

    // ------------------------------------------------------------------
    // Lists stay lean
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Client list and agent expiring list do not expose details")]
    public async Task Lists_DoNotExposeDetails()
    {
        await AdminPostEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            EntityDocBody("ADM-LIST-024", Details(DetailsEmpNo)));

        var clientList = await ScopeClient.SendAsync(ScopeGet(
            "/api/v1/client/documents?page=1&pageSize=20",
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(HttpStatusCode.OK, clientList.StatusCode);
        AssertListItemsHideDetails(
            await clientList.Content.ReadAsStringAsync(),
            expectedAtLeast: 1);

        var agentList = await ScopeClient.SendAsync(ScopeGet(
            "/api/v1/agent/documents/expiring?days=30",
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.OK, agentList.StatusCode);
        AssertListItemsHideDetails(
            await agentList.Content.ReadAsStringAsync(),
            expectedAtLeast: 1);
    }

    [Fact(DisplayName =
        "Admin extracting a document gets an extraction draft")]
    public async Task Admin_Extract_DraftCreated()
    {
        var json = await ExtractAsync("admin", TestDataSeeder.Ids.Admin, "Admin");

        Assert.NotEqual(Guid.Empty, ExtractId(json));
    }

    [Fact(DisplayName =
        "Agent extract POST response exposes the Mock's schema-field suggestions")]
    public async Task Agent_Extract_ResponseIncludesEmptySuggestedDetails()
    {
        var json = await ExtractAsync("agent", TestDataSeeder.Ids.AgentA, "Agent");

        AssertDetailsEqual(
            MockPassportDetails,
            Navigate(json, "data", "suggestedDetails"));
    }

    [Fact(DisplayName =
        "Agent extraction GET exposes the Mock's schema-field suggestions")]
    public async Task Agent_GetExtraction_ExposesEmptySuggestedDetails()
    {
        var json = await ExtractAsync("agent", TestDataSeeder.Ids.AgentA, "Agent");
        var extractionId = ExtractId(json);

        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/agent/documents/extractions/{extractionId}",
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertDetailsEqual(
            MockPassportDetails,
            Navigate(await response.Content.ReadAsStringAsync(), "data", "suggestedDetails"));
    }

    // The Mock fills issue and expiry dates; for a Passport these are schema
    // fields, so they also appear in the details, as MRZ-read dates do.
    private const string MockPassportDetails =
        "{\"issueDate\":\"2026-01-01\",\"expiryDate\":\"2031-01-01\"}";
}