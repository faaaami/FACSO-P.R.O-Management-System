using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Concurrency;

/// <summary>
/// Task 7B.6.3: confirmation concurrency hardening (Option C). Both the Admin
/// and Agent confirmation handlers now BEGIN an explicit transaction, lock the
/// DocumentExtraction row with SELECT ... FOR UPDATE, and only then run the
/// existing Pending check + document creation + status change + audits in one
/// atomic SaveChanges/Commit. This closes the same-extraction double-confirm
/// race WITHOUT changing the duplicate guard or the database schema:
///
///   Request A -> 201 (Document created once)
///   Request B -> 409 (observes Status = "Confirmed" after the row lock)
///
/// The cross-extraction duplicate race (two DIFFERENT extractions producing the
/// same owner/type/document-number) is intentionally OUT of scope for this task.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ConfirmationConcurrencyTests : IntegrationTestBase
{
    private const string AdminConfirmUrl =
        "/api/v1/admin/documents/extractions/{0}/confirm";

    private const string AgentConfirmUrl =
        "/api/v1/agent/documents/extractions/{0}/confirm";

    public ConfirmationConcurrencyTests(DatabaseFixture fixture)
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
                    SuggestedDocumentNumber = "DOC-SUG-001",
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
        Guid? clientEntityId = null)
        => new
        {
            employeeId = employeeId ?? TestDataSeeder.Ids.EmployeeA,
            clientEntityId,
            type = DocumentType.Passport,
            documentNumber,
            issueDate = "2023-01-01T00:00:00Z",
            expiryDate = "2032-01-01T00:00:00Z",
            contentType = "application/pdf",
            fileSize = 2048,
            details = new Dictionary<string, object?>
            {
                ["passportNumber"] = documentNumber
            }
        };

    private Task<HttpResponseMessage> AdminConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            string.Format(AdminConfirmUrl, extractionId),
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> AgentConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            string.Format(AgentConfirmUrl, extractionId),
            body,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString()));

    private sealed record ExtractionState(
        string Status,
        int DocumentCount,
        int ConfirmationAuditCount,
        int CreatedAuditCount);

    private async Task<ExtractionState> StateAsync(Guid extractionId)
    {
        var state = new ExtractionState("", 0, 0, 0);

        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .First(x => x.Id == extractionId);

            // The confirmed Document copies the extraction's FileUrl (server
            // reference) verbatim, and it is unique per created extraction,
            // so it cleanly identifies ONLY the document(s) this flow created
            // - never the seeder's DocumentA/DocumentB or their audits.
            var createdDocumentIds = db.Documents
                .Where(x => !x.IsDeleted && x.FileUrl == extraction.FileUrl)
                .Select(x => x.Id)
                .ToList();

            state = new ExtractionState(
                extraction.Status,
                createdDocumentIds.Count,
                db.AuditLogs.Count(x =>
                    x.EntityType == "DocumentExtraction" &&
                    x.Action == "DocumentExtractionConfirmed" &&
                    x.EntityId == extractionId),
                db.AuditLogs.Count(x =>
                    x.Action == "DocumentCreated" &&
                    createdDocumentIds.Contains(x.EntityId)));
        });

        return state;
    }

    private sealed class ConfirmEnvelope
    {
        public bool Success { get; set; }

        public ConfirmData? Data { get; set; }
    }

    private sealed class ConfirmData
    {
        public Guid Id { get; set; }

        public string DocumentNumber { get; set; } = string.Empty;
    }

    private static void AssertExactlyOne201Rest409(
        IReadOnlyList<HttpStatusCode> statuses,
        int expectedConflictCount)
    {
        Assert.Equal(1, statuses.Count(x => x == HttpStatusCode.Created));
        Assert.Equal(
            expectedConflictCount,
            statuses.Count(x => x == HttpStatusCode.Conflict));
    }

    // ==================================================================
    // Admin concurrency
    // ==================================================================

    [Fact(DisplayName =
        "Admin: 3 concurrent confirms of the same extraction produce one 201 and two 409")]
    public async Task Admin_ConcurrentConfirmSameExtraction_OneWins()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId);

        var body = ConfirmBody("DOC-CONCUR-01");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 3)
                .Select(_ => Task.Run(
                    () => AdminConfirmAsync(extractionId, body)))
                .ToArray());

        AssertExactlyOne201Rest409(
            responses.Select(r => r.StatusCode).ToList(),
            expectedConflictCount: 2);

        var state = await StateAsync(extractionId);

        Assert.Equal("Confirmed", state.Status);
        Assert.Equal(1, state.DocumentCount);
        Assert.Equal(1, state.ConfirmationAuditCount);
        Assert.Equal(1, state.CreatedAuditCount);
    }

    [Fact(DisplayName =
        "Admin: concurrent confirms with DIFFERENT payloads still produce one document")]
    public async Task Admin_ConcurrentConfirmDifferentPayload_OneDocument()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId);

        var bodies = new[]
        {
            ConfirmBody("P-ONE"),
            ConfirmBody("P-TWO")
        };

        var responses = await Task.WhenAll(
            bodies.Select(body => Task.Run(
                () => AdminConfirmAsync(extractionId, body)))
                .ToArray());

        AssertExactlyOne201Rest409(
            responses.Select(r => r.StatusCode).ToList(),
            expectedConflictCount: 1);

        // The winner's payload must be the ONLY document materialised.
        var winnerResponse = responses.Single(
            r => r.StatusCode == HttpStatusCode.Created);
        var winner =
            await ReadJsonAsync<ConfirmEnvelope>(winnerResponse);

        Assert.NotNull(winner?.Data);

        string winnerNumber = string.Empty;
        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .First(x => x.Id == extractionId);

            var document = db.Documents.Single(
                x => !x.IsDeleted && x.FileUrl == extraction.FileUrl);
            winnerNumber = document.DocumentNumber;
        });

        Assert.Contains(winner.Data!.DocumentNumber, new[] { "P-ONE", "P-TWO" });
        Assert.Equal(winner.Data.DocumentNumber, winnerNumber);

        var state = await StateAsync(extractionId);

        Assert.Equal("Confirmed", state.Status);
        Assert.Equal(1, state.DocumentCount);
        Assert.Equal(1, state.ConfirmationAuditCount);
        Assert.Equal(1, state.CreatedAuditCount);
    }

    [Fact(DisplayName =
        "Admin: a post-lock SaveChanges failure rolls everything back")]
    public async Task Admin_FirstConfirmationFailureRollsBack()
    {
        var extractionId = Guid.NewGuid();

        // The extraction may hold a file name longer than the documents.file_name
        // column (varchar(255)); the extracted row's name is copied verbatim into
        // the created Document, so the final SaveChanges fails deterministically
        // AFTER the extraction row has been locked - never before.
        await SeedExtractionAsync(
            extractionId,
            fileName: new string('A', 300));

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody("DOC-FAIL-01"));

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        // The explicit transaction must have rolled the failure back:
        // extraction still Pending, no Document, no confirmation audits.
        var state = await StateAsync(extractionId);

        Assert.Equal("Pending", state.Status);
        Assert.Equal(0, state.DocumentCount);
        Assert.Equal(0, state.ConfirmationAuditCount);
        Assert.Equal(0, state.CreatedAuditCount);
    }

    [Fact(DisplayName =
        "Admin: sequential double confirmation still yields 201 then 409")]
    public async Task Admin_SequentialConfirmThenConfirm_Still409()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId);

        var body = ConfirmBody("DOC-SEQ-01");

        var first = await AdminConfirmAsync(extractionId, body);
        var second = await AdminConfirmAsync(extractionId, body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var state = await StateAsync(extractionId);

        Assert.Equal("Confirmed", state.Status);
        Assert.Equal(1, state.DocumentCount);
    }

    [Fact(DisplayName =
        "Admin: confirming an already Confirmed extraction creates no second document")]
    public async Task Admin_AlreadyProcessedStill409_NoSecondDocument()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId);

        var body = ConfirmBody("DOC-DONE-01");

        var first = await AdminConfirmAsync(extractionId, body);
        var second = await AdminConfirmAsync(extractionId, body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var state = await StateAsync(extractionId);

        Assert.Equal("Confirmed", state.Status);
        Assert.Equal(1, state.DocumentCount);
        Assert.Equal(1, state.ConfirmationAuditCount);
        Assert.Equal(1, state.CreatedAuditCount);
    }

    [Fact(DisplayName =
        "Admin: expired extraction still becomes Expired with audit and returns 409")]
    public async Task Admin_ExpiredExtraction_Still409WithAudit()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            expiresInHours: -1);

        var response = await AdminConfirmAsync(
            extractionId,
            ConfirmBody("DOC-EXP-01"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var state = await StateAsync(extractionId);

        // The Expired transition + DocumentExtractionExpired audit were
        // committed before the 409 was raised (existing observable behavior).
        Assert.Equal("Expired", state.Status);
        Assert.Equal(0, state.DocumentCount);
        Assert.Equal(0, state.ConfirmationAuditCount);
        Assert.Equal(0, state.CreatedAuditCount);

        await WithScopeDbContextAsync(db =>
            Assert.Equal(1, db.AuditLogs.Count(x =>
                x.EntityType == "DocumentExtraction" &&
                x.Action == "DocumentExtractionExpired" &&
                x.EntityId == extractionId)));
    }

    // ==================================================================
    // Agent concurrency
    // ==================================================================

    [Fact(DisplayName =
        "Agent: concurrent confirms of the same extraction produce one 201 and one 409")]
    public async Task Agent_ConcurrentConfirmSameExtraction_OneWins()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentA);

        var body = ConfirmBody("DOC-AGENT-CONCUR");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 2)
                .Select(_ => Task.Run(
                    () => AgentConfirmAsync(extractionId, body)))
                .ToArray());

        AssertExactlyOne201Rest409(
            responses.Select(r => r.StatusCode).ToList(),
            expectedConflictCount: 1);

        var state = await StateAsync(extractionId);

        Assert.Equal("Confirmed", state.Status);
        Assert.Equal(1, state.DocumentCount);
        Assert.Equal(1, state.ConfirmationAuditCount);
        Assert.Equal(1, state.CreatedAuditCount);
    }

    [Fact(DisplayName =
        "Agent: sequential second confirmation still returns 409")]
    public async Task Agent_AlreadyProcessedStill409()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentA);

        var body = ConfirmBody("DOC-AGENT-SEQ");

        var first = await AgentConfirmAsync(extractionId, body);
        var second = await AgentConfirmAsync(extractionId, body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var state = await StateAsync(extractionId);

        Assert.Equal("Confirmed", state.Status);
        Assert.Equal(1, state.DocumentCount);
    }
}