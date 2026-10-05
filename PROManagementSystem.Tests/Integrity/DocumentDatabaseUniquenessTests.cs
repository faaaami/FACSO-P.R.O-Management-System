using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Integrity;

/// <summary>
/// Task 7B.6.4: database-level cross-extraction duplicate integrity.
///
/// The AddDocumentUniqueness migration creates a partial unique index over
/// (COALESCE(client_entity_id, employee_id), type, document_number) for active
/// documents, and a central translator maps the resulting PostgreSQL 23505 into
/// the application's DuplicateDocumentException (409 DUPLICATE_DOCUMENT). These
/// tests prove the behavior end to end for the cross-extraction race and the
/// admin/agent update paths, plus the ownership/type/case soft-delete and
/// unrelated-violation semantics the constraint must preserve.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentDatabaseUniquenessTests : IntegrationTestBase
{
    private const string AdminConfirmUrl =
        "/api/v1/admin/documents/extractions/{0}/confirm";

    private const string AdminUpdateUrl =
        "/api/v1/admin/documents/{0}";

    private const string AdminDeleteUrl =
        "/api/v1/admin/documents/{0}";

    private const string AgentConfirmUrl =
        "/api/v1/agent/documents/extractions/{0}/confirm";

    private const string AgentUpdateUrl =
        "/api/v1/agent/documents/{0}";

    public DocumentDatabaseUniquenessTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private Task SeedExtractionAsync(
        Guid extractionId,
        Guid? createdBy = null,
        string fileName = "upload.pdf",
        string? fileUrl = null,
        string status = "Pending",
        int expiresInHours = 24)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(
                new DocumentExtraction
                {
                    Id = extractionId,
                    FileName = fileName,
                    FileUrl = fileUrl ??
                              $"https://storage.test/{extractionId:N}/extraction",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    SuggestedType = DocumentType.Passport,
                    SuggestedDocumentNumber = "DOC-DB-SUGGESTED",
                    SuggestedIssueDate = null,
                    SuggestedExpiryDate = null,
                    SuggestedOwnerType = "Employee",
                    Confidence = 0.95m,
                    Status = status,
                    ExpiresAt = now.AddHours(expiresInHours),
                    CreatedBy = createdBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

    private static object ConfirmBody(
        string documentNumber,
        Guid? employeeId = null,
        Guid? clientEntityId = null,
        DocumentType type = DocumentType.Passport)
    {
        var requiredKey =
            type == DocumentType.Visa
                ? "visaNumber"
                : "passportNumber";

        return new
        {
            employeeId = employeeId ?? TestDataSeeder.Ids.EmployeeA,
            clientEntityId,
            type = type,
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            contentType = "application/pdf",
            fileSize = 2048,
            details = new Dictionary<string, object?>
            {
                [requiredKey] = documentNumber
            }
        };
    }

    private static object AdminUpdateBody(
        string documentNumber)
        => new
        {
            type = DocumentType.Passport,
            documentNumber,
            issueDate = "2020-06-01T00:00:00Z",
            expiryDate = "2035-01-01T00:00:00Z",
            fileUrl = "https://storage.test/doc-a.pdf",
            fileName = "doc-a.pdf",
            contentType = "application/pdf",
            fileSize = 4096,
            isActive = true,
            details = (object?)null
        };

    private Task<HttpResponseMessage> AdminConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            string.Format(AdminConfirmUrl, extractionId),
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AdminUpdateAsync(
        Guid documentId,
        object body)
        => ScopeClient.SendAsync(ScopePatch(
            string.Format(AdminUpdateUrl, documentId),
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AdminDeleteAsync(
        Guid documentId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            string.Format(AdminDeleteUrl, documentId));

        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                TestDataSeeder.Ids.Admin, "Admin", null));

        return ScopeClient.SendAsync(request);
    }

    private Task<HttpResponseMessage> AgentUpdateAsync(
        Guid documentId,
        object body)
        => ScopeClient.SendAsync(ScopePatch(
            string.Format(AgentUpdateUrl, documentId),
            body,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString()));

    private async Task<List<Document>> ActiveDocumentsAsync(
        Guid? employeeId,
        Guid? clientEntityId,
        DocumentType type,
        string documentNumber)
    {
        var matches = new List<Document>();

        await WithScopeDbContextAsync(db =>
        {
            matches = db.Documents
                .Where(x => !x.IsDeleted
                            && x.EmployeeId == employeeId
                            && x.ClientEntityId == clientEntityId
                            && x.Type == type
                            && x.DocumentNumber == documentNumber)
                .ToList();
        });

        return matches;
    }

    private async Task<int> TotalRowsForKeyAsync(
        Guid? employeeId,
        Guid? clientEntityId,
        DocumentType type,
        string documentNumber)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.Documents.IgnoreQueryFilters()
                .Count(x => x.EmployeeId == employeeId
                            && x.ClientEntityId == clientEntityId
                            && x.Type == type
                            && x.DocumentNumber == documentNumber);
        });

        return count;
    }

    private async Task<string?> ExtractionStatusAsync(Guid extractionId)
    {
        string? status = null;

        await WithScopeDbContextAsync(db =>
        {
            status = db.DocumentExtractions
                .First(x => x.Id == extractionId)
                .Status;
        });

        return status;
    }

    private sealed class ConfirmEnvelope
    {
        public bool Success { get; set; }

        public ConfirmData? Data { get; set; }
    }

    private sealed class ConfirmData
    {
        public Guid DocumentId { get; set; }
    }

    private sealed class ErrorEnvelope
    {
        public ApiErrorShape? Error { get; set; }
    }

    private sealed class ApiErrorShape
    {
        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    private static async Task<string> ErrorCodeAsync(
        HttpResponseMessage response)
    {
        var envelope = await ReadJsonAsync<ErrorEnvelope>(response);
        return envelope?.Error?.Code ?? string.Empty;
    }

    // ==================================================================
    // A. Cross-extraction concurrency: the unique index is the backstop
    // ==================================================================

    [Fact(DisplayName =
        "A: two extractions confirmed concurrently for the same key yield one 201 and one 409")]
    public async Task A_ConcurrentCrossExtractionConfirm_OneWins()
    {
        var extractionA = Guid.NewGuid();
        var extractionB = Guid.NewGuid();

        await SeedExtractionAsync(extractionA);
        await SeedExtractionAsync(extractionB);

        var body = ConfirmBody("DOC-DB-RACE-01");

        var responses = await Task.WhenAll(
            Task.Run(() => AdminConfirmAsync(extractionA, body)),
            Task.Run(() => AdminConfirmAsync(extractionB, body)));

        var statuses = responses.Select(r => r.StatusCode).ToList();

        Assert.Equal(1, statuses.Count(x => x == HttpStatusCode.Created));
        Assert.Equal(1, statuses.Count(x => x == HttpStatusCode.Conflict));

        var conflict = responses.Single(
            r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(
            "DUPLICATE_DOCUMENT",
            await ErrorCodeAsync(conflict));

        // Exactly one active document exists for the key.
        var documents = await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "DOC-DB-RACE-01");
        Assert.Single(documents);

        // The losing request left no partial state: its extraction remains
        // Pending and no second confirmation audit exists.
        var statusesAfter = new[]
        {
            await ExtractionStatusAsync(extractionA),
            await ExtractionStatusAsync(extractionB)
        };

        Assert.Equal(1, statusesAfter.Count(x => x == "Confirmed"));
        Assert.Equal(1, statusesAfter.Count(x => x == "Pending"));

        var confirmationAuditCount = 0;
        await WithScopeDbContextAsync(db =>
        {
            confirmationAuditCount = db.AuditLogs.Count(x =>
                x.EntityType == "DocumentExtraction" &&
                x.Action == "DocumentExtractionConfirmed" &&
                (x.EntityId == extractionA || x.EntityId == extractionB));
        });
        Assert.Equal(1, confirmationAuditCount);
    }

    // ==================================================================
    // B. Admin update to a duplicate number -> 409, document unchanged
    // ==================================================================

    [Fact(DisplayName =
        "B: admin updating a document to an existing number returns 409 and leaves it unchanged")]
    public async Task B_AdminUpdateToDuplicateNumber_409AndUnchanged()
    {
        // Create an active document carrying the target key (EmployeeA,
        // Passport, DOC-DB-ADMIN-01).
        var primaryExtraction = Guid.NewGuid();
        await SeedExtractionAsync(primaryExtraction);

        var primary = await AdminConfirmAsync(
            primaryExtraction,
            ConfirmBody("DOC-DB-ADMIN-01"));
        Assert.Equal(HttpStatusCode.Created, primary.StatusCode);

        // Admin tries to update the seeded DocumentA (EmployeeA, Passport,
        // DOC-A-001) to reuse that number.
        var response = await AdminUpdateAsync(
            TestDataSeeder.Ids.DocumentA,
            AdminUpdateBody("DOC-DB-ADMIN-01"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "DUPLICATE_DOCUMENT",
            await ErrorCodeAsync(response));

        // DocumentA is untouched and the only active document for the
        // target key is still the originally confirmed one.
        var documentAState = new { Number = "", IssueDate = (DateTime?)null };
        await WithScopeDbContextAsync(db =>
        {
            var documentA = db.Documents.First(x => x.Id == TestDataSeeder.Ids.DocumentA);
            documentAState = new { Number = documentA.DocumentNumber, IssueDate = documentA.IssueDate };
        });

        Assert.Equal("DOC-A-001", documentAState.Number);

        var target = await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "DOC-DB-ADMIN-01");
        Assert.Single(target);
    }

    // ==================================================================
    // C. Agent update (PATCH) to a duplicate number -> 409, unchanged
    // ==================================================================

    [Fact(DisplayName =
        "C: agent PATCHing a document number onto another active document returns 409 and leaves it unchanged")]
    public async Task C_AgentUpdateToDuplicateNumber_409AndUnchanged()
    {
        // Create an active document with the number the agent will try to steal.
        var primaryExtraction = Guid.NewGuid();
        await SeedExtractionAsync(primaryExtraction);

        var primary = await AdminConfirmAsync(
            primaryExtraction,
            ConfirmBody("DOC-DB-AGENT-01"));
        Assert.Equal(HttpStatusCode.Created, primary.StatusCode);

        // DocumentA is in Agent A's scope (TaskA -> DocumentA, Company A).
        var response = await AgentUpdateAsync(
            TestDataSeeder.Ids.DocumentA,
            new { documentNumber = "DOC-DB-AGENT-01" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "DUPLICATE_DOCUMENT",
            await ErrorCodeAsync(response));

        // DocumentA keeps its own number.
        await WithScopeDbContextAsync(db =>
        {
            var documentA = db.Documents.First(x => x.Id == TestDataSeeder.Ids.DocumentA);
            Assert.Equal("DOC-A-001", documentA.DocumentNumber);
        });
    }

    // ==================================================================
    // D. Soft-delete then recreate the same key -> allowed, one active
    // ==================================================================

    [Fact(DisplayName =
        "D: recreating a soft-deleted key is allowed and yields exactly one active document")]
    public async Task D_SoftDeleteThenRecreateKey_Allowed()
    {
        var firstExtraction = Guid.NewGuid();
        await SeedExtractionAsync(firstExtraction);

        var first = await AdminConfirmAsync(
            firstExtraction,
            ConfirmBody("DOC-DB-RECREATE-01"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var created = (await ReadJsonAsync<ConfirmEnvelope>(first))!.Data!;

        // Soft-delete it.
        var deleteResponse = await AdminDeleteAsync(created.DocumentId);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Recreate the same owner/type/number from a fresh extraction.
        var secondExtraction = Guid.NewGuid();
        await SeedExtractionAsync(secondExtraction);

        var second = await AdminConfirmAsync(
            secondExtraction,
            ConfirmBody("DOC-DB-RECREATE-01"));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        // Exactly one ACTIVE row for the key, but two physical rows.
        var active = await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "DOC-DB-RECREATE-01");
        Assert.Single(active);
        Assert.Equal(2, await TotalRowsForKeyAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "DOC-DB-RECREATE-01"));
    }

    // ==================================================================
    // E. Different owner, same type + number -> allowed
    // ==================================================================

    [Fact(DisplayName =
        "E: the same number under a different owner is allowed")]
    public async Task E_DifferentOwnerSameNumber_Allowed()
    {
        var extractionA = Guid.NewGuid();
        var extractionB = Guid.NewGuid();

        await SeedExtractionAsync(extractionA);
        await SeedExtractionAsync(extractionB);

        var forEmployeeA = await AdminConfirmAsync(
            extractionA,
            ConfirmBody(
                "DOC-DB-OWNER-01",
                employeeId: TestDataSeeder.Ids.EmployeeA));
        var forEmployeeB = await AdminConfirmAsync(
            extractionB,
            ConfirmBody(
                "DOC-DB-OWNER-01",
                employeeId: TestDataSeeder.Ids.EmployeeB));

        Assert.Equal(HttpStatusCode.Created, forEmployeeA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, forEmployeeB.StatusCode);

        Assert.Single(await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "DOC-DB-OWNER-01"));
        Assert.Single(await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeB,
            null,
            DocumentType.Passport,
            "DOC-DB-OWNER-01"));
    }

    // ==================================================================
    // F. Same owner + number, different type -> allowed
    // ==================================================================

    [Fact(DisplayName =
        "F: the same number under a different type is allowed")]
    public async Task F_DifferentTypeSameOwnerNumber_Allowed()
    {
        var passportExtraction = Guid.NewGuid();
        var visaExtraction = Guid.NewGuid();

        await SeedExtractionAsync(passportExtraction);
        await SeedExtractionAsync(visaExtraction);

        var passport = await AdminConfirmAsync(
            passportExtraction,
            ConfirmBody(
                "DOC-DB-TYPE-01",
                employeeId: TestDataSeeder.Ids.EmployeeA,
                type: DocumentType.Passport));
        var visa = await AdminConfirmAsync(
            visaExtraction,
            ConfirmBody(
                "DOC-DB-TYPE-01",
                employeeId: TestDataSeeder.Ids.EmployeeA,
                type: DocumentType.Visa));

        Assert.Equal(HttpStatusCode.Created, passport.StatusCode);
        Assert.Equal(HttpStatusCode.Created, visa.StatusCode);

        Assert.Single(await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "DOC-DB-TYPE-01"));
        Assert.Single(await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Visa,
            "DOC-DB-TYPE-01"));
    }

    // ==================================================================
    // G. Case sensitivity: ABC-123 and abc-123 are distinct keys
    // ==================================================================

    [Fact(DisplayName =
        "G: the number comparison is case-sensitive, so both casing variants coexist")]
    public async Task G_CaseSensitiveNumbers_BothAllowed()
    {
        var upperExtraction = Guid.NewGuid();
        var lowerExtraction = Guid.NewGuid();

        await SeedExtractionAsync(upperExtraction);
        await SeedExtractionAsync(lowerExtraction);

        var upper = await AdminConfirmAsync(
            upperExtraction,
            ConfirmBody(
                "ABC-123",
                employeeId: TestDataSeeder.Ids.EmployeeA));
        var lower = await AdminConfirmAsync(
            lowerExtraction,
            ConfirmBody(
                "abc-123",
                employeeId: TestDataSeeder.Ids.EmployeeA));

        Assert.Equal(HttpStatusCode.Created, upper.StatusCode);
        Assert.Equal(HttpStatusCode.Created, lower.StatusCode);

        Assert.Single(await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "ABC-123"));
        Assert.Single(await ActiveDocumentsAsync(
            TestDataSeeder.Ids.EmployeeA,
            null,
            DocumentType.Passport,
            "abc-123"));
    }

    // ==================================================================
    // H. An unrelated unique violation stays a DB error, not DUPLICATE_DOCUMENT
    // ==================================================================

    [Fact(DisplayName =
        "H: an unrelated unique violation is not translated into DUPLICATE_DOCUMENT")]
    public async Task H_UnrelatedUniqueViolation_NotTranslated()
    {
        var translator = Fixture.Factory.Services
            .GetRequiredService<IDuplicateDocumentTranslator>();

        await WithScopeDbContextAsync(db =>
        {
            var document = new Document
            {
                Id = Guid.NewGuid(),
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.Passport,
                DocumentNumber = "DOC-DB-H-01",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(4),
                FileUrl = "https://storage.test/h-doc",
                FileName = "h-doc.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                IsDeleted = false,
                DeletedAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Documents.Add(document);

            db.DocumentVersions.Add(new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                VersionNumber = 1,
                FileUrl = "https://storage.test/v1",
                FileName = "v1.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                IsDeleted = false,
                DeletedAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            db.DocumentVersions.Add(new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                VersionNumber = 1,
                FileUrl = "https://storage.test/v2",
                FileName = "v2.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                IsDeleted = false,
                DeletedAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            try
            {
                db.SaveChanges();
                Assert.Fail(
                    "Expected a unique violation on " +
                    "(document_id, version_number).");
            }
            catch (DbUpdateException exception)
            {
                // The constraint name is IX_document_versions_document_id_
                // version_number, not the document uniqueness constraint, so the
                // translator must decline to translate it.
                Assert.Null(translator.Translate(exception));
            }
        });
    }
}