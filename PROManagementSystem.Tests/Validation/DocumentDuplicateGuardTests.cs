using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Proves the duplicate-document guard: an active, non-deleted document with
/// the same owner + type + (outer-trimmed, case-sensitive) document number
/// blocks confirmation and registration with a 409 DUPLICATE_DOCUMENT.
/// A different type, a different owner, case differences, and soft-deleted
/// existing documents never block; rejection persists nothing and leaves the
/// extraction Pending.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentDuplicateGuardTests : IntegrationTestBase
{
    public DocumentDuplicateGuardTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private Task SeedExtractionAsync(
        Guid extractionId,
        Guid? createdBy = null)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(
                new DocumentExtraction
                {
                    Id = extractionId,
                    FileName = "upload.pdf",
                    FileUrl =
                        $"extractions/{Guid.NewGuid():N}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
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

    private Task SeedDocumentAsync(
        Guid id,
        Guid? employeeId,
        Guid? clientEntityId,
        DocumentType type,
        string documentNumber,
        bool isDeleted = false)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(
                new Document
                {
                    Id = id,
                    EmployeeId = employeeId,
                    ClientEntityId = clientEntityId,
                    Type = type,
                    DocumentNumber = documentNumber,
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddYears(4),
                    FileUrl = "https://storage.test/dummy",
                    FileName = "doc.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = isDeleted,
                    DeletedAt = isDeleted ? now : null
                });
            db.SaveChanges();
        });

    private static Dictionary<string, object?> RequiredDetails(
        DocumentType type)
    {
        var key = type switch
        {
            DocumentType.Passport => "passportNumber",
            DocumentType.Visa => "visaNumber",
            DocumentType.EmiratesId => "idNumber",
            DocumentType.LaborCard => "laborCardNumber",
            DocumentType.TradeLicense => "licenseNumber",
            DocumentType.EstablishmentCard => "establishmentNumber",
            _ => "passportNumber"
        };

        return new Dictionary<string, object?>
        {
            [key] = "REQ-001"
        };
    }

    private object ConfirmBody(
        Guid? employeeId,
        Guid? clientEntityId,
        DocumentType type,
        string documentNumber)
        => new
        {
            employeeId,
            clientEntityId,
            type = type.ToString(),
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            details = RequiredDetails(type)
        };

    private object RegisterBody(
        DocumentType type,
        string documentNumber)
        => new
        {
            type = type.ToString(),
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z"
        };

    private Task<HttpResponseMessage> AdminConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/admin/documents/extractions/" +
            $"{extractionId}/confirm",
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AgentConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/agent/documents/extractions/" +
            $"{extractionId}/confirm",
            body,
            TestDataSeeder.Ids.AgentA,
            "Agent"));

    private static async Task AssertDuplicateRejectedAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("DUPLICATE_DOCUMENT", body);
    }

    private Task AssertExtractionStatusAsync(
        Guid extractionId,
        string expectedStatus)
        => WithScopeDbContextAsync(db =>
        {
            Assert.Equal(
                expectedStatus,
                db.DocumentExtractions
                    .First(x => x.Id == extractionId)
                    .Status);
        });

    private Task AssertDocumentCountAsync(
        string documentNumber,
        int expectedCount)
        => WithScopeDbContextAsync(db =>
        {
            Assert.Equal(
                expectedCount,
                db.Documents.Count(
                    x => x.DocumentNumber == documentNumber));
        });

    // ------------------------------------------------------------------
    // Admin duplicate: employee document
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin confirming a duplicate employee document returns 409 " +
        "DUPLICATE_DOCUMENT, creates nothing, and leaves the extraction Pending")]
    public async Task Admin_DuplicateEmployeeDocument_Returns409()
    {
        var extractionId = Guid.NewGuid();
        const string number = "DUP-ADMIN-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            number);
        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                DocumentType.Passport,
                number));

        await AssertDuplicateRejectedAsync(response);
        await AssertExtractionStatusAsync(extractionId, "Pending");
        await AssertDocumentCountAsync(number, 1);
    }

    // ------------------------------------------------------------------
    // Agent duplicate: employee document
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent confirming a duplicate employee document returns 409 " +
        "DUPLICATE_DOCUMENT, creates nothing, and leaves the extraction Pending")]
    public async Task Agent_DuplicateEmployeeDocument_Returns409()
    {
        var extractionId = Guid.NewGuid();
        const string number = "DUP-AGENT-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            number);
        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentA);

        var response = await AgentConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                DocumentType.Passport,
                number));

        await AssertDuplicateRejectedAsync(response);
        await AssertExtractionStatusAsync(extractionId, "Pending");
        await AssertDocumentCountAsync(number, 1);
    }

    // ------------------------------------------------------------------
    // Duplicate: entity document
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Confirming a duplicate entity document returns 409 DUPLICATE_DOCUMENT")]
    public async Task Admin_DuplicateEntityDocument_Returns409()
    {
        var extractionId = Guid.NewGuid();
        const string number = "DUP-ENTITY-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            null,
            TestDataSeeder.Ids.EntityA,
            DocumentType.TradeLicense,
            number);
        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                null,
                TestDataSeeder.Ids.EntityA,
                DocumentType.TradeLicense,
                number));

        await AssertDuplicateRejectedAsync(response);
        await AssertExtractionStatusAsync(extractionId, "Pending");
        await AssertDocumentCountAsync(number, 1);
    }

    // ------------------------------------------------------------------
    // Same number, different type -> allowed
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Same document number with a different DocumentType is allowed")]
    public async Task SameDocumentNumber_DifferentType_IsAllowed()
    {
        var extractionId = Guid.NewGuid();
        const string number = "MONO-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            number);
        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                DocumentType.Visa,
                number));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertExtractionStatusAsync(extractionId, "Confirmed");
        await AssertDocumentCountAsync(number, 2);
    }

    // ------------------------------------------------------------------
    // Same type/number, different owner -> allowed
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Same DocumentType and document number under a different owner is allowed")]
    public async Task SameTypeAndNumber_DifferentOwner_IsAllowed()
    {
        var extractionId = Guid.NewGuid();
        const string number = "OWNER-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            number);
        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeB,
                null,
                DocumentType.Passport,
                number));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertExtractionStatusAsync(extractionId, "Confirmed");
        await AssertDocumentCountAsync(number, 2);
    }

    // ------------------------------------------------------------------
    // Soft-deleted existing document -> allowed
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A soft-deleted existing document does not block a new one")]
    public async Task SoftDeletedExistingDocument_NewDocumentIsAllowed()
    {
        var extractionId = Guid.NewGuid();
        const string number = "SOFT-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            number,
            isDeleted: true);
        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                DocumentType.Passport,
                number));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertExtractionStatusAsync(extractionId, "Confirmed");

        // The global query filter only sees the newly created document.
        await AssertDocumentCountAsync(number, 1);
    }

    // ------------------------------------------------------------------
    // Case / whitespace semantics (trimmed, case-sensitive)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Whitespace-only differences are treated as the same number and rejected")]
    public async Task WhitespaceOnlyDifferences_AreRejectedAsDuplicate()
    {
        var extractionId = Guid.NewGuid();
        const string number = "WS-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            number);
        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                DocumentType.Passport,
                $"  {number}  "));

        await AssertDuplicateRejectedAsync(response);
        await AssertExtractionStatusAsync(extractionId, "Pending");
        await AssertDocumentCountAsync(number, 1);
    }

    [Fact(DisplayName =
        "Case differences are distinct numbers and allowed (case-sensitive comparison)")]
    public async Task CaseVariants_AreAllowed()
    {
        var extractionId = Guid.NewGuid();
        const string number = "CASE-001";

        await SeedDocumentAsync(
            Guid.NewGuid(),
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            number);
        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                DocumentType.Passport,
                number.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertExtractionStatusAsync(extractionId, "Confirmed");
        await AssertDocumentCountAsync(number, 1);
        await AssertDocumentCountAsync(number.ToLowerInvariant(), 1);
    }

    // ------------------------------------------------------------------
    // Registration / create paths
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin register-employee-document rejects a duplicate owner/type/number")]
    public async Task AdminRegisterEmployeeDocument_Duplicate_Returns409()
    {
        const string number = "REG-EMP-001";
        var employeeId = TestDataSeeder.Ids.EmployeeA;

        await SeedDocumentAsync(
            Guid.NewGuid(),
            employeeId,
            null,
            DocumentType.Passport,
            number);

        var response = await ScopeClient.SendAsync(ScopePost(
            $"/api/v1/admin/employees/{employeeId}/documents",
            RegisterBody(DocumentType.Passport, number),
            TestDataSeeder.Ids.Admin,
            "Admin"));

        await AssertDuplicateRejectedAsync(response);
        await AssertDocumentCountAsync(number, 1);
    }

    [Fact(DisplayName =
        "Agent register-entity-document rejects a duplicate owner/type/number")]
    public async Task AgentRegisterEntityDocument_Duplicate_Returns409()
    {
        const string number = "REG-ENT-001";
        var entityId = TestDataSeeder.Ids.EntityA;

        await SeedDocumentAsync(
            Guid.NewGuid(),
            null,
            entityId,
            DocumentType.TradeLicense,
            number);

        var response = await ScopeClient.SendAsync(ScopePost(
            $"/api/v1/agent/entities/{entityId}/documents",
            RegisterBody(DocumentType.TradeLicense, number),
            TestDataSeeder.Ids.AgentA,
            "Agent"));

        await AssertDuplicateRejectedAsync(response);
        await AssertDocumentCountAsync(number, 1);
    }

    // ------------------------------------------------------------------
    // Control: successful confirmation behavior unchanged
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A unique confirmation still succeeds unchanged")]
    public async Task ExistingSuccessfulConfirmation_RemainsUnchanged()
    {
        var extractionId = Guid.NewGuid();
        const string number = "FRESH-001";

        await SeedExtractionAsync(extractionId);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                DocumentType.Passport,
                number));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertExtractionStatusAsync(extractionId, "Confirmed");
        await AssertDocumentCountAsync(number, 1);
    }
}