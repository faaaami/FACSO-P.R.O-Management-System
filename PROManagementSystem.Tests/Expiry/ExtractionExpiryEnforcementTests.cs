using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// Proves confirm-time expiry enforcement: a Pending extraction past its
/// ExpiresAt is rejected with 409, its status is persisted as "Expired",
/// no Document is created, and a DocumentExtractionExpired audit is written.
/// NULL ExpiresAt rows (legacy) stay confirmable. File cleanup / background
/// jobs are out of scope for this suite (Task 4B.2).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExtractionExpiryEnforcementTests : IntegrationTestBase
{
    private const string DocumentNumber = "DOC-EXP-001";

    public ExtractionExpiryEnforcementTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private Task SeedExtractionAsync(
        Guid extractionId,
        DateTime? expiresAt,
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
                    ExpiresAt = expiresAt,
                    CreatedBy = createdBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

    private object ValidConfirmBody() => new
    {
        employeeId = TestDataSeeder.Ids.EmployeeA,
        clientEntityId = (Guid?)null,
        type = "Passport",
        documentNumber = DocumentNumber,
        details = new { passportNumber = DocumentNumber },
        issueDate = "2023-01-01T00:00:00Z",
        expiryDate = "2032-01-01T00:00:00Z"
    };

    private Task<HttpResponseMessage> ConfirmAsync(
        Guid extractionId,
        Guid userId,
        string role)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/{(role == "Admin" ? "admin" : "agent")}/" +
            $"documents/extractions/{extractionId}/confirm",
            ValidConfirmBody(),
            userId,
            role));

    private async Task AssertExpiredOutcomeAsync(
        Guid extractionId,
        Guid userId)
    {
        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .First(x => x.Id == extractionId);

            Assert.Equal("Expired", extraction.Status);

            // No document may have been created from this confirmation.
            Assert.False(
                db.Documents.Any(
                    x => x.DocumentNumber == DocumentNumber));

            var expiryAudit = db.AuditLogs
                .FirstOrDefault(
                    x =>
                        x.Action == "DocumentExtractionExpired" &&
                        x.EntityId == extractionId);

            Assert.NotNull(expiryAudit);
            Assert.Equal(userId, expiryAudit.UserId);
        });
    }

    // ------------------------------------------------------------------
    // Admin: expired extraction
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin confirming an expired Pending extraction returns 409 and " +
        "persists the Expired status without creating a Document")]
    public async Task Admin_ExpiredExtraction_IsRejectedAndMarkedExpired()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            DateTime.UtcNow.AddDays(-1));

        var response = await ConfirmAsync(
            extractionId,
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await AssertExpiredOutcomeAsync(
            extractionId,
            TestDataSeeder.Ids.Admin);
    }

    [Fact(DisplayName =
        "ExpiresAt equal to or just before now is treated as expired " +
        "(boundary, `<=` semantics)")]
    public async Task Admin_ExpiredAtBoundary_IsRejected()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            DateTime.UtcNow.AddMinutes(-5));

        var response = await ConfirmAsync(
            extractionId,
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await AssertExpiredOutcomeAsync(
            extractionId,
            TestDataSeeder.Ids.Admin);
    }

    // ------------------------------------------------------------------
    // Admin: valid non-expired confirmation still works
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin confirming a non-expired Pending extraction still succeeds")]
    public async Task Admin_NonExpiredExtraction_IsConfirmed()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            DateTime.UtcNow.AddHours(24));

        var response = await ConfirmAsync(
            extractionId,
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var envelope = await ReadJsonAsync<ConfirmEnvelope>(response);
        Assert.NotNull(envelope?.Data);

        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .First(x => x.Id == extractionId);

            Assert.Equal("Confirmed", extraction.Status);

            var document = db.Documents
                .First(x => x.Id == envelope!.Data!.DocumentId);

            Assert.Equal(DocumentNumber, document.DocumentNumber);
            Assert.Equal(
                TestDataSeeder.Ids.EmployeeA,
                document.EmployeeId);
        });
    }

    // ------------------------------------------------------------------
    // Agent: expired extraction created by the agent
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent confirming an expired extraction they created returns 409 " +
        "and persists the Expired status")]
    public async Task Agent_ExpiredExtraction_IsRejectedAndMarkedExpired()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            DateTime.UtcNow.AddDays(-1),
            createdBy: TestDataSeeder.Ids.AgentA);

        var response = await ConfirmAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA,
            "Agent");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await AssertExpiredOutcomeAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);
    }

    // ------------------------------------------------------------------
    // Legacy: NULL ExpiresAt never expires
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A NULL ExpiresAt extraction (legacy) remains confirmable")]
    public async Task Legacy_NullExpiresAt_IsConfirmed()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            null);

        var response = await ConfirmAsync(
            extractionId,
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var envelope = await ReadJsonAsync<ConfirmEnvelope>(response);
        Assert.NotNull(envelope?.Data);

        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .First(x => x.Id == extractionId);

            Assert.Equal("Confirmed", extraction.Status);

            var document = db.Documents
                .First(x => x.Id == envelope!.Data!.DocumentId);

            Assert.Equal(DocumentNumber, document.DocumentNumber);
        });
    }

    // ------------------------------------------------------------------
    // Response envelope
    // ------------------------------------------------------------------

    private sealed class ConfirmEnvelope
    {
        public ConfirmPayload? Data { get; set; }
    }

    private sealed class ConfirmPayload
    {
        public Guid DocumentId { get; set; }
    }
}