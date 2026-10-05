using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Task 7B.5: type-specific Details validation through the real HTTP pipeline.
///
/// Value-kind + MaxLength checks run on every write path that supplies a
/// non-empty Details object (create/register/update/PATCH/confirm). Required
/// fields are enforced on confirmation only: confirming a document of a type
/// with a required identifier while Details is empty/omitted/null is a 400,
/// whereas creating/registering the same payload succeeds. DocumentType.Other
/// and unhandled enum values are exempt because their schema is empty, and
/// unknown keys are always allowed and preserved.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentDetailsSchemaValidationTests : IntegrationTestBase
{
    public DocumentDetailsSchemaValidationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Request-body helpers
    // ------------------------------------------------------------------

    private static Dictionary<string, object?> PassportDetails(
        bool withPassportNumber = true)
    {
        var details = new Dictionary<string, object?>
        {
            ["nationality"] = "Indian"
        };

        if (withPassportNumber)
        {
            details["passportNumber"] = "P-OK-001";
        }

        return details;
    }

    private static object ConfirmBody(
        Guid? employeeId,
        Guid? entityId,
        string documentType,
        string documentNumber,
        object? details)
        => new
        {
            employeeId,
            clientEntityId = entityId,
            type = documentType,
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            details
        };

    private static object EntityDocBody(
        string documentNumber,
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
            fileSize = 2048,
            details
        };

    private static object UpdateBody(
        string documentNumber,
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
            isActive = true,
            details
        };

    // ------------------------------------------------------------------
    // Seed / HTTP helpers
    // ------------------------------------------------------------------

    private Task SeedExtractionAsync(
        Guid extractionId,
        DocumentType suggestedType,
        Guid? createdBy = null)
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
                    SuggestedType = suggestedType,
                    SuggestedDocumentNumber = "SUG-001",
                    SuggestedDetails = "{}",
                    Status = "Pending",
                    ExpiresAt = now.AddHours(24),
                    CreatedBy = createdBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

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

    private Task<HttpResponseMessage> AdminCreateEntityDocumentAsync(
        Guid entityId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/admin/entities/{entityId}/documents",
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private async Task<HttpResponseMessage> AdminUpdateEntityDocumentAsync(
        Guid entityId,
        object body)
    {
        var create = await AdminCreateEntityDocumentAsync(
            entityId,
            EntityDocBody("SCHEMA-UPD-001", PassportDetails()));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var json = await create.Content.ReadAsStringAsync();
        var id = Navigate(json, "data").GetProperty("id").GetGuid();

        return await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/admin/documents/{id}",
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));
    }

    private static async Task AssertValidation400Async(
        HttpResponseMessage response,
        string propertyName)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
        Assert.Contains(propertyName, body);
    }

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

    // ------------------------------------------------------------------
    // Required applies on confirmation only
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin confirming a Passport without passportNumber is a 400")]
    public async Task AdminConfirm_MissingRequired_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-001",
                details: null));

        await AssertValidation400Async(response, "Details.passportNumber");
    }

    [Fact(DisplayName =
        "Admin confirming a Passport with an empty Details object is a 400")]
    public async Task AdminConfirm_EmptyObject_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-002",
                new Dictionary<string, object?>()));

        await AssertValidation400Async(response, "Details.passportNumber");
    }

    [Fact(DisplayName =
        "Agent confirming a Passport without passportNumber is a 400")]
    public async Task AgentConfirm_MissingRequired_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            DocumentType.Passport,
            createdBy: TestDataSeeder.Ids.AgentA);

        var response = await AgentConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-003",
                new Dictionary<string, object?>()));

        await AssertValidation400Async(response, "Details.passportNumber");
    }

    [Fact(DisplayName =
        "Admin confirming a Passport whose passportNumber is JSON null is a 400")]
    public async Task AdminConfirm_NullRequiredValue_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-004",
                new Dictionary<string, object?>
                {
                    ["passportNumber"] = null
                }));

        await AssertValidation400Async(response, "Details.passportNumber");
    }

    [Fact(DisplayName =
        "Admin confirming a Passport with a valid passportNumber is a 201")]
    public async Task AdminConfirm_ValidPassport_Returns201()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var details = PassportDetails();

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-OK-005",
                details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(
            "P-OK-001",
            Navigate(json, "data", "details").GetProperty("passportNumber").GetString());

        var documentId = Navigate(json, "data").GetProperty("documentId").GetGuid();

        var stored = string.Empty;
        await WithScopeDbContextAsync(db =>
            stored = db.Documents
                .AsNoTracking()
                .Single(x => x.Id == documentId)
                .Details);

        using var storedDoc = JsonDocument.Parse(stored);
        Assert.Equal(
            "P-OK-001",
            storedDoc.RootElement.GetProperty("passportNumber").GetString());
    }

    [Fact(DisplayName =
        "Creating a Passport without passportNumber succeeds (required is confirm-only)")]
    public async Task Create_MissingRequired_Succeeds()
    {
        var response = await AdminCreateEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            EntityDocBody(
                "SCHEMA-CRT-006",
                new Dictionary<string, object?>()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Value-kind + MaxLength apply on every write path
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Confirming a Passport with a non-string nationality is a 400")]
    public async Task Confirm_WrongTypedStringField_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var details = PassportDetails();
        details["nationality"] = 123;

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-007",
                details));

        await AssertValidation400Async(response, "Details.nationality");
    }

    [Fact(DisplayName =
        "Confirming a Passport with a malformed date is a 400")]
    public async Task Confirm_InvalidDate_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var details = PassportDetails();
        details["dateOfBirth"] = "2001/04/10";

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-008",
                details));

        await AssertValidation400Async(response, "Details.dateOfBirth");
    }

    [Fact(DisplayName =
        "Confirming a Passport with an over-length passportNumber is a 400")]
    public async Task Confirm_OverMaxLength_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var details = PassportDetails();
        details["passportNumber"] = new string('P', 51);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-009",
                details));

        await AssertValidation400Async(response, "Details.passportNumber");
    }

    [Fact(DisplayName =
        "Confirming a Passport with a 50-char passportNumber succeeds")]
    public async Task Confirm_AtMaxLength_Succeeds()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var details = PassportDetails();
        details["passportNumber"] = new string('P', 50);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-010",
                details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName =
        "Creating a Passport with a non-string nationality is a 400")]
    public async Task Create_WrongTypedStringField_Returns400()
    {
        var details = PassportDetails();
        details["nationality"] = 123;

        var response = await AdminCreateEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            EntityDocBody("SCHEMA-CRT-011", details));

        await AssertValidation400Async(response, "Details.nationality");
    }

    [Fact(DisplayName =
        "Admin updating a Passport with a non-string nationality is a 400")]
    public async Task Update_WrongTypedStringField_Returns400()
    {
        var details = PassportDetails();
        details["nationality"] = 123;

        var response = await AdminUpdateEntityDocumentAsync(
            TestDataSeeder.Ids.EntityA,
            UpdateBody("SCHEMA-UPD-012", details));

        await AssertValidation400Async(response, "Details.nationality");
    }

    [Fact(DisplayName =
        "Agent PATCHing a document with a non-string nationality is a 400")]
    public async Task AgentPatch_WrongTypedStringField_Returns400()
    {
        var details = PassportDetails();
        details["nationality"] = 123;

        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { details },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        await AssertValidation400Async(response, "Details.nationality");
    }

    [Fact(DisplayName =
        "Agent PATCHing a document with valid passport details succeeds")]
    public async Task AgentPatch_ValidDetails_Succeeds()
    {
        var response = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { details = PassportDetails() },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Unknown keys allowed & preserved; Other exempt
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Confirming a Passport with unknown extra keys succeeds and preserves them")]
    public async Task Confirm_UnknownKeys_ArePreserved()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Passport);

        var details = PassportDetails();
        details["vendorSpecific"] = new Dictionary<string, object?>
        {
            ["trackingRef"] = "TRACK-001"
        };

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Passport",
                "SCHEMA-CNF-013",
                details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(
            "TRACK-001",
            Navigate(json, "data", "details")
                .GetProperty("vendorSpecific")
                .GetProperty("trackingRef")
                .GetString());
    }

    [Fact(DisplayName =
        "Confirming an Other document with the empty object succeeds")]
    public async Task Confirm_Other_EmptyObject_Succeeds()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Other);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Other",
                "SCHEMA-OTH-014",
                new Dictionary<string, object?>()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName =
        "Confirming an Other document with arbitrary Details succeeds")]
    public async Task Confirm_Other_ArbitraryDetails_Succeeds()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, DocumentType.Other);

        var details = new Dictionary<string, object?>
        {
            ["anything"] = "goes",
            ["nested"] = new Dictionary<string, object?> { ["deep"] = true },
            ["n"] = 5
        };

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "Other",
                "SCHEMA-OTH-015",
                details));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}