using System.Net;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Security;

/// <summary>
/// Documents the REPORTED-ONLY MEDIUM gap in <see cref="DocumentExtraction"/>:
/// it has no <c>CreatedBy</c> column, and <c>AgentConfirmExtractionCommandHandler</c>
/// only verifies that the SELECTED owner (employee/entity) is in the acting
/// agent's task-linked scope. It does not check that the extraction was
/// created by the acting agent. Consequently, when two agents share a task on
/// the same owner, either agent can confirm the other's pending extraction.
/// The user instructed that this gap is report-only and must NOT be fixed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SecurityGapTests : IntegrationTestBase
{
    public SecurityGapTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid ExtractionId =
        Guid.Parse("00000000-0000-0000-0000-000000000701");

    private async Task SeedPendingExtractionAsync(
        string fileName,
        DocumentType suggestedType)
    {
        await WithScopeDbContextAsync(db =>
        {
            db.DocumentExtractions.Add(new DocumentExtraction
            {
                Id = ExtractionId,
                FileName = fileName,
                FileUrl = "https://storage.test/tmp",
                ContentType = "application/pdf",
                FileSize = 5120,
                SuggestedType = suggestedType,
                SuggestedDocumentNumber = "AI-EXTRACT",
                Confidence = 0.95m,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });
    }

    [Fact(DisplayName =
        "GAP (report-only): an agent can confirm a Pending extraction whose " +
        "owner is in their scope even if another agent initiated it (201).")]
    public async Task AgentConfirmsPendingExtraction_InScope_Returns201()
    {
        // Simulate: extraction uploaded by Agent B for Employee A. Because
        // DocumentExtraction has no CreatedBy, nothing ties the extraction
        // to Agent B. Agent A has Task A -> Document A -> Employee A, so the
        // owner-scope check passes and Agent A may confirm Agent B's draft.
        await SeedPendingExtractionAsync(
            "extracted-employee-a.pdf", DocumentType.Passport);

        var request = ScopePost(
            $"/api/v1/agent/documents/extractions/{ExtractionId}/confirm",
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = DocumentType.Passport,
                documentNumber = "DOC-NEW-A-001"
            },
            TestDataSeeder.Ids.AgentA);

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName =
        "An agent cannot confirm an extraction whose owner is OUT of scope (404)")]
    public async Task AgentConfirmsExtraction_OutOfScopeOwner_Returns404()
    {
        await SeedPendingExtractionAsync(
            "extracted-employee-b.pdf", DocumentType.Passport);

        var request = ScopePost(
            $"/api/v1/agent/documents/extractions/{ExtractionId}/confirm",
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeB,
                clientEntityId = (Guid?)null,
                type = DocumentType.Passport,
                documentNumber = "DOC-B-001"
            },
            TestDataSeeder.Ids.AgentA);

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Confirming a non-Pending (already processed) extraction returns 409")]
    public async Task ConfirmAlreadyProcessedExtraction_Returns409()
    {
        var extractionId = Guid.Parse(
            "00000000-0000-0000-0000-000000000702");

        await WithScopeDbContextAsync(db =>
        {
            db.DocumentExtractions.Add(new DocumentExtraction
            {
                Id = extractionId,
                FileName = "processed.pdf",
                FileUrl = "https://storage.test/tmp",
                ContentType = "application/pdf",
                FileSize = 4096,
                SuggestedType = DocumentType.Visa,
                Status = "Confirmed",
                Confidence = 0.9m,
                ExpiresAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var request = ScopePost(
            $"/api/v1/agent/documents/extractions/{extractionId}/confirm",
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = DocumentType.Visa,
                documentNumber = "DOC-ALREADY"
            },
            TestDataSeeder.Ids.AgentA);

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
