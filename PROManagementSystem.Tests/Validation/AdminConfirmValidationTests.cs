using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Proves the admin confirmation surface (POST /api/v1/admin/documents
/// /extractions/{id}/confirm) rejects invalid input with a 400 and that a
/// valid manual confirmation still creates a real Document — preserving the
/// server-generated storage reference (Task 2B) as the authoritative FileUrl.
/// Mirrors the agent-side semantics asserted by ValidationSemanticsTests.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AdminConfirmValidationTests : IntegrationTestBase
{
    public AdminConfirmValidationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private Task SeedExtractionAsync(
        Guid extractionId,
        string fileUrl)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(
                new DocumentExtraction
                {
                    Id = extractionId,
                    FileName = "upload.pdf",
                    FileUrl = fileUrl,
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    SuggestedType = DocumentType.Passport,
                    SuggestedDocumentNumber = "DOC-SUG-001",
                    SuggestedIssueDate = null,
                    SuggestedExpiryDate = null,
                    SuggestedOwnerType = "Employee",
                    Confidence = 0.95m,
                    Status = "Pending",
                    ExpiresAt = now.AddHours(24),
                    CreatedBy = null,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

    private Task<HttpResponseMessage> ConfirmAsync(
        Guid extractionId,
        object body)
        => ScopeClient.SendAsync(ScopePost(
            $"/api/v1/admin/documents/extractions/" +
            $"{extractionId}/confirm",
            body,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private static async Task AssertValidation400Async(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
    }

    private static async Task Assert400Not500Async(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("INTERNAL_SERVER_ERROR", body);
    }

    private object ValidConfirmBody() => new
    {
        employeeId = TestDataSeeder.Ids.EmployeeA,
        clientEntityId = (Guid?)null,
        type = "Passport",
        documentNumber = "DOC-MAN-001",
        issueDate = "2023-01-01T00:00:00Z",
        expiryDate = "2032-01-01T00:00:00Z",
        fileUrl = "https://storage.test/fake-client-url",
        fileName = "client-supplied.pdf",
        contentType = "application/pdf",
        fileSize = 2048,
        details = new Dictionary<string, object?>
        {
            ["passportNumber"] = "P-MAN-001"
        }
    };

    // ------------------------------------------------------------------
    // Invalid input -> 400
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin confirming an extraction without a documentNumber is a 400")]
    public async Task A_MissingDocumentNumber_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            $"extractions/{Guid.NewGuid():N}.pdf");

        var response = await ConfirmAsync(
            extractionId,
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = "Passport"
            });

        // For DTOs with non-nullable required members the 400 is produced
        // upfront by ASP.NET Core model-state validation (problem-details
        // body) before the command reaches the pipeline validator. Either
        // body is acceptable; the guarantee asserted is 400, never 500.
        await Assert400Not500Async(response);
    }

    [Fact(DisplayName =
        "Admin confirming an extraction with a whitespace-only " +
        "documentNumber is a 400")]
    public async Task B_WhitespaceOnlyDocumentNumber_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            $"extractions/{Guid.NewGuid():N}.pdf");

        var response = await ConfirmAsync(
            extractionId,
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = "Passport",
                documentNumber = "   "
            });

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "Admin confirming an extraction with an undefined DocumentType " +
        "numeric value is a 400")]
    public async Task C_UndefinedDocumentTypeNumeric_Returns400()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            $"extractions/{Guid.NewGuid():N}.pdf");

        var response = await ConfirmAsync(
            extractionId,
            new
            {
                employeeId = TestDataSeeder.Ids.EmployeeA,
                clientEntityId = (Guid?)null,
                type = 7,
                documentNumber = "DOC-MAN-001"
            });

        await Assert400Not500Async(response);
    }

    // ------------------------------------------------------------------
    // Valid confirmation still succeeds
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A valid admin confirmation creates a Dashboard document, " +
        "confirming the extraction, and carries the server generation " +
        "reference as the authoritative FileUrl")]
    public async Task Valid_AdminConfirmation_CreatesDocumentWithServerReference()
    {
        var extractionId = Guid.NewGuid();
        var serverReference =
            $"extractions/{Guid.NewGuid():N}.pdf";

        await SeedExtractionAsync(
            extractionId,
            serverReference);

        var response = await ConfirmAsync(
            extractionId,
            ValidConfirmBody());

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var envelope =
            await ReadJsonAsync<ConfirmEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        Assert.NotEqual(Guid.Empty, envelope!.Data!.DocumentId);

        // The response identifies the extraction that was confirmed.
        Assert.Equal(extractionId, envelope.Data.ExtractionId);

        // Submitted values are the final document values.
        Assert.Equal("DOC-MAN-001", envelope.Data.DocumentNumber);
        Assert.Equal(DocumentType.Passport, envelope.Data.Type);
        Assert.Equal(TestDataSeeder.Ids.EmployeeA, envelope.Data.EmployeeId);
        Assert.Null(envelope.Data.ClientEntityId);

        // Task 2B: the server-generated reference survives manual
        // confirmation; the client-supplied fake URL is ignored.
        Assert.Equal(serverReference, envelope.Data.FileUrl);

        // Persisted side effects.
        var now = DateTime.UtcNow;

        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents
                .First(x => x.Id == envelope!.Data!.DocumentId);

            Assert.Equal("DOC-MAN-001", document.DocumentNumber);
            Assert.Equal(DocumentType.Passport, document.Type);
            Assert.Equal(
                TestDataSeeder.Ids.EmployeeA,
                document.EmployeeId);
            Assert.Null(document.ClientEntityId);
            Assert.Equal(serverReference, document.FileUrl);
            Assert.True(document.IsActive);
            Assert.False(document.IsDeleted);
            Assert.True(
                document.CreatedAt <= now &&
                document.CreatedAt > now.AddMinutes(-1));

            var extraction = db.DocumentExtractions
                .First(x => x.Id == extractionId);

            Assert.Equal("Confirmed", extraction.Status);
            Assert.Equal(serverReference, extraction.FileUrl);
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

        public Guid ExtractionId { get; set; }

        public Guid? EmployeeId { get; set; }

        public Guid? ClientEntityId { get; set; }

        public DocumentType Type { get; set; }

        public string DocumentNumber { get; set; } = string.Empty;

        public string? FileUrl { get; set; }
    }
}