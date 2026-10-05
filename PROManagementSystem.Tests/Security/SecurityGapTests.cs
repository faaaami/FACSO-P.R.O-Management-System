using System.Net;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Security;

/// <summary>
/// Documents the evolved <see cref="DocumentExtraction"/> ownership behavior
/// after the Day 6 H1 fix. H1 added a nullable <c>CreatedBy</c> column: an
/// extraction created by Agent X can only be confirmed by Agent X (any other
/// agent gets 404 regardless of owner scope). Rows where <c>CreatedBy IS
/// NULL</c> are treated LEGACY and keep the pre-H1 behavior: any agent whose
/// owner scope passes may confirm them. The tests below lock in the legacy
/// NULL-row path plus the unchanged out-of-scope and already-processed
/// guarantees; the ownership-specific matrix lives in
/// <see cref="ExtractionOwnershipTests"/>.
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
        "LEGACY (H1): a NULL CreatedBy extraction row keeps pre-H1 behavior - " +
        "any in-scope agent may confirm it (201).")]
    public async Task AgentConfirmsPendingExtraction_InScope_Returns201()
    {
        // Simulate a legacy row created before the H1 CreatedBy column
        // existed: CreatedBy is NULL, so no ownership gate applies and the
        // pre-H1 scope-only check governs confirmation.
        await SeedPendingExtractionAsync(
            "extracted-employee-a.pdf", DocumentType.Passport);

        var request = ScopePost(
            $"/api/v1/agent/documents/extractions/{ExtractionId}/confirm",
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = DocumentType.Passport,
                documentNumber = "DOC-NEW-A-001",
                details = new { passportNumber = "P-NEW-A-001" }
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
                documentNumber = "DOC-B-001",
                details = new { passportNumber = "P-B-001" }
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
                documentNumber = "DOC-ALREADY",
                details = new { visaNumber = "V-ALREADY-001" }
            },
            TestDataSeeder.Ids.AgentA);

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
