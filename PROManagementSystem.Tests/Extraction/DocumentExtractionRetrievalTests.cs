using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 5B.2: read-only retrieval of document extraction drafts.
/// Admin may read any non-deleted extraction. An agent may only read drafts
/// they created (CreatedBy == caller); missing, soft-deleted, foreign-owned,
/// and legacy NULL CreatedBy rows all look like 404. GET never mutates state:
/// no status change, no Document, no AuditLog. Expired Pending drafts remain
/// retrievable; expiry enforcement stays in the confirm/cleanup paths.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentExtractionRetrievalTests : IntegrationTestBase
{
    private const string ContentTypePdf = "application/pdf";
    private const long FileSize = 2048;

    private static readonly byte[] PdfBytes =
    [
        0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
        0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
    ];

    public DocumentExtractionRetrievalTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static DateTime Normalize(DateTime value)
        => value.AddTicks(-(value.Ticks % 10));

    private Task SeedExtractionAsync(
        Guid extractionId,
        Guid? createdBy,
        string status = "Pending",
        DateTime? expiresAt = null,
        bool isDeleted = false)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(
                new DocumentExtraction
                {
                    Id = extractionId,
                    FileName = "upload.pdf",
                    FileUrl =
                        $"extractions/{extractionId:N}.pdf",
                    ContentType = ContentTypePdf,
                    FileSize = FileSize,
                    SuggestedType = DocumentType.Passport,
                    SuggestedDocumentNumber = "SUG-001",
                    SuggestedIssueDate =
                        new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    SuggestedExpiryDate =
                        new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    SuggestedOwnerType = "Employee",
                    Confidence = 0.87m,
                    Status = status,
                    ExpiresAt = expiresAt.HasValue
                        ? Normalize(expiresAt.Value)
                        : null,
                    CreatedBy = createdBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = isDeleted,
                    DeletedAt = isDeleted
                        ? now.AddHours(-1)
                        : null
                });
            db.SaveChanges();
        });

    private async Task<Guid> ExtractAsync(
        Guid userId,
        string role = "Agent",
        string? clientCompanyId = null)
    {
        using var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType =
            new MediaTypeHeaderValue(ContentTypePdf);

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", "upload.pdf");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/agent/documents/extract")
        {
            Content = form
        };
        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                userId, role, clientCompanyId));

        var response = await ScopeClient.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope =
            await ReadJsonAsync<ExtractEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        return envelope!.Data!.ExtractionId;
    }

    private async Task<HttpResponseMessage> GetAsync(
        Guid extractionId,
        Guid userId,
        string? role = null)
        => await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/" +
                $"{(role is not null ? $"admin/documents" : "agent/documents")}" +
                $"/extractions/{extractionId}",
                userId,
                role ?? "Agent"));

    // ------------------------------------------------------------------
    // Admin
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin retrieves an existing extraction (200) with all fields")]
    public async Task Admin_GetExistingExtraction_Returns200_WithAllFields()
    {
        var extractionId = Guid.NewGuid();
        var expiresAt = Normalize(DateTime.UtcNow.AddHours(10));
        await SeedExtractionAsync(extractionId, createdBy: null, expiresAt: expiresAt);

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.Admin,
            role: "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope =
            await ReadJsonAsync<GetExtractionEnvelope>(response);

        var data = envelope?.Data;
        Assert.NotNull(data);
        Assert.True(envelope!.Success);
        Assert.Equal(extractionId, data.ExtractionId);

        // D1 removed the storage reference from the extraction read contracts:
        // the review payload is fetched before anyone has decided to open the
        // file, and the dedicated /file endpoint streams the bytes on demand. The
        // dedicated assertions that the property is absent live in
        // PendingDocumentExtractionListingTests (ExtractionDetailOmitsFileUrl /
        // AgentExtractionDetailOmitsFileUrl); this one only has to agree that the
        // field does not arrive, rather than expecting the seeded value back.
        Assert.Null(data.FileUrl);
        Assert.Equal("upload.pdf", data.FileName);
        Assert.Equal(ContentTypePdf, data.ContentType);
        Assert.Equal(FileSize, data.FileSize);
        Assert.Equal("Passport", data.SuggestedType);
        Assert.Equal("SUG-001", data.SuggestedDocumentNumber);
        Assert.Equal(
            new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            data.SuggestedIssueDate);
        Assert.Equal(
            new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            data.SuggestedExpiryDate);
        Assert.Equal("Employee", data.SuggestedOwnerType);
        Assert.Equal(0.87m, data.Confidence);
        Assert.Equal("Pending", data.Status);
        Assert.Equal(expiresAt, data.ExpiresAt);
    }

    [Fact(DisplayName =
        "Admin retrieving a missing extraction returns 404")]
    public async Task Admin_GetMissingExtraction_Returns404()
    {
        var response = await GetAsync(
            TestDataSeeder.Ids.Unknown,
            TestDataSeeder.Ids.Admin,
            role: "Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Admin retrieving a soft-deleted extraction returns 404")]
    public async Task Admin_GetDeletedExtraction_Returns404()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            createdBy: null,
            isDeleted: true);

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.Admin,
            role: "Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Agent
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent retrieves its own extraction created through the real extract endpoint (200)")]
    public async Task Agent_GetOwnExtraction_Returns200()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await WithScopeDbContextAsync(db =>
        {
            Assert.Equal(
                TestDataSeeder.Ids.AgentA,
                db.DocumentExtractions
                    .AsNoTracking()
                    .Single(x => x.Id == extractionId)
                    .CreatedBy);
        });

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope =
            await ReadJsonAsync<GetExtractionEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        Assert.Equal("Pending", envelope!.Data!.Status);
    }

    [Fact(DisplayName =
        "Agent retrieving another agent's extraction returns 404")]
    public async Task Agent_GetAnotherAgentsExtraction_Returns404()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentB);

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent retrieving a legacy NULL CreatedBy extraction returns 404")]
    public async Task Agent_GetLegacyNullCreatedByExtraction_Returns404()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(extractionId, createdBy: null);

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent retrieving a missing extraction returns 404")]
    public async Task Agent_GetMissingExtraction_Returns404()
    {
        var response = await GetAsync(
            TestDataSeeder.Ids.Unknown,
            TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent retrieving a soft-deleted extraction returns 404")]
    public async Task Agent_GetDeletedExtraction_Returns404()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentA,
            isDeleted: true);

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Read-only + expiry semantics
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "An expired Pending extraction remains retrievable (200)")]
    public async Task ExpiredPendingExtraction_RemainsRetrievable_Returns200()
    {
        var extractionId = Guid.NewGuid();
        var pastExpiry = Normalize(DateTime.UtcNow.AddHours(-2));

        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentA,
            status: "Pending",
            expiresAt: pastExpiry);

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope =
            await ReadJsonAsync<GetExtractionEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        Assert.Equal("Pending", envelope!.Data!.Status);
        Assert.Equal(pastExpiry, envelope.Data.ExpiresAt);
    }

    [Fact(DisplayName =
        "GET does not change the extraction Status or UpdatedAt")]
    public async Task Get_DoesNotChangeExtractionStatus()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentA);

        var before = await WithScopeRowAsync(extractionId);

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);

        await EnsureOk(response, HttpStatusCode.OK);

        var after = await WithScopeRowAsync(extractionId);

        Assert.Equal("Pending", after.Status);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    }

    [Fact(DisplayName =
        "GET does not create a Document or an AuditLog")]
    public async Task Get_DoesNotCreateDocumentOrAuditLog()
    {
        var extractionId = Guid.NewGuid();
        await SeedExtractionAsync(
            extractionId,
            createdBy: TestDataSeeder.Ids.AgentA);

        var (documentCountBefore, auditCountBefore) =
            await WithScopeCountsAsync();

        var response = await GetAsync(
            extractionId,
            TestDataSeeder.Ids.AgentA);

        await EnsureOk(response, HttpStatusCode.OK);

        var (documentCountAfter, auditCountAfter) =
            await WithScopeCountsAsync();

        Assert.Equal(documentCountBefore, documentCountAfter);
        Assert.Equal(auditCountBefore, auditCountAfter);
    }

    private async Task<(string Status, DateTime UpdatedAt)>
        WithScopeRowAsync(Guid extractionId)
    {
        string status = string.Empty;
        var updatedAt = default(DateTime);

        await WithScopeDbContextAsync(db =>
        {
            var row = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId);
            status = row.Status;
            updatedAt = row.UpdatedAt;
        });

        return (status, updatedAt);
    }

    private async Task<(int Documents, int AuditLogs)>
        WithScopeCountsAsync()
    {
        var documents = 0;
        var auditLogs = 0;

        await WithScopeDbContextAsync(db =>
        {
            documents = db.Documents.Count();
            auditLogs = db.AuditLogs.Count();
        });

        return (documents, auditLogs);
    }

    // ------------------------------------------------------------------
    // Deserialization envelopes
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

    private sealed class GetExtractionEnvelope
    {
        public bool Success { get; set; }

        public GetExtractionPayload? Data { get; set; }
    }

    private sealed class GetExtractionPayload
    {
        public Guid ExtractionId { get; set; }

        public string? FileUrl { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string? SuggestedType { get; set; }

        public string? SuggestedDocumentNumber { get; set; }

        public DateTime? SuggestedIssueDate { get; set; }

        public DateTime? SuggestedExpiryDate { get; set; }

        public string? SuggestedOwnerType { get; set; }

        public decimal Confidence { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }
    }
}