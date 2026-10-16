using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7B.6.1: protected extraction file download endpoints.
/// Admin may download any non-deleted extraction's file. An agent may only
/// download files for extractions they created (CreatedBy == caller). The
/// endpoint streams bytes directly (never a JSON envelope), uses the stored
/// validated ContentType, never honors a client-supplied FileName as a path,
/// and returns 404 for missing/deleted/traversal/empty references.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExtractionFileDownloadTests : IntegrationTestBase
{
    private const string AdminFileUrl =
        "/api/v1/admin/documents/extractions/{0}/file";

    private const string AgentFileUrl =
        "/api/v1/agent/documents/extractions/{0}/file";

    private static readonly byte[] PdfBytes =
    [
        0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
        0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
    ];

    public ExtractionFileDownloadTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private string FactoryStorageRoot =>
        ((TestApplicationFactory)Factory).StorageRoot;

    private string Resolve(string reference)
        => Path.Combine(
            FactoryStorageRoot,
            reference.Replace(
                '/',
                Path.DirectorySeparatorChar));

    private static HttpRequestMessage ScopedPost(
        string url,
        HttpContent? content,
        Guid userId,
        string role,
        string? clientCompanyId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                userId, role, clientCompanyId));

        return request;
    }

    private static MultipartFormDataContent PdfUpload(
        string fileName = "passport.pdf")
    {
        var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        form.Add(new StringContent("Passport"), "documentType");
        return form;
    }

    private async Task<Guid> ExtractAsync(
        Guid userId,
        string role,
        string? companyId = null,
        string fileName = "passport.pdf")
    {
        var url = role == "Admin"
            ? "/api/v1/admin/documents/extract"
            : "/api/v1/agent/documents/extract";

        var response = await ScopeClient.SendAsync(
            ScopedPost(
                url,
                PdfUpload(fileName),
                userId,
                role,
                companyId));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope =
            await ReadJsonAsync<ExtractEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        return envelope!.Data!.ExtractionId;
    }

    private async Task<HttpResponseMessage> DownloadAsync(
        string url,
        Guid userId,
        string role,
        string? clientCompanyId = null)
        => await ScopeClient.SendAsync(
            ScopeGet(url, userId, role, clientCompanyId));

    private Task SetExtractionPropertyAsync(
        Guid extractionId,
        Action<DocumentExtraction> mutate)
        => WithScopeDbContextAsync(db =>
        {
            var row = db.DocumentExtractions.Single(x => x.Id == extractionId);
            mutate(row);
            db.SaveChanges();
        });

    // ------------------------------------------------------------------
    // Admin API
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin can download its own extraction's file (200, exact bytes)")]
    public async Task AdminCanDownloadOwnExtractionFile()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);
        Assert.Equal(
            "application/pdf",
            response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(PdfBytes, bytes);
    }

    [Fact(DisplayName =
        "Missing extraction returns 404")]
    public async Task AdminMissingExtraction_Returns404()
    {
        var response = await DownloadAsync(
            string.Format(AdminFileUrl, TestDataSeeder.Ids.Unknown),
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Soft-deleted extraction returns 404")]
    public async Task AdminDeletedExtraction_Returns404()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        await SetExtractionPropertyAsync(extractionId, row =>
        {
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
        });

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Physical file removed by cleanup returns 404")]
    public async Task AdminMissingPhysicalFile_Returns404()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        string? reference = null;

        await WithScopeDbContextAsync(db =>
        {
            reference = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId)
                .FileUrl;
        });

        var fullPath = Resolve(reference!);
        Assert.True(File.Exists(fullPath));
        File.Delete(fullPath);

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "NULL FileUrl returns not-available (404)")]
    public async Task AdminNullFileUrl_Returns404()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        await SetExtractionPropertyAsync(extractionId, row =>
            row.FileUrl = null);

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Download Content-Type matches the stored validated type")]
    public async Task AdminContentTypeMatchesStoredType()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        string? storedContentType = null;
        await WithScopeDbContextAsync(db =>
        {
            storedContentType = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId)
                .ContentType;
        });

        Assert.Equal(
            storedContentType,
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName =
        "Returned bytes match the stored file on disk")]
    public async Task AdminReturnedBytesMatchStoredFile()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        string? reference = null;
        await WithScopeDbContextAsync(db =>
        {
            reference = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId)
                .FileUrl;
        });

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        var served = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(PdfBytes, served);
        Assert.Equal(PdfBytes, File.ReadAllBytes(Resolve(reference!)));
    }

    [Fact(DisplayName =
        "FileName is safe for Content-Disposition (no header injection)")]
    public async Task AdminFileNameSafeForContentDisposition()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        const string malicious = "scan\r\nX-Injected: yes.pdf";
        await SetExtractionPropertyAsync(extractionId, row =>
            row.FileName = malicious);

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        var hasDisposition =
            response.Content.Headers.TryGetValues(
                "Content-Disposition", out var values);

        if (hasDisposition)
        {
            foreach (var value in values!)
            {
                Assert.DoesNotContain("\r", value, StringComparison.Ordinal);
                Assert.DoesNotContain("\n", value, StringComparison.Ordinal);
            }
        }
    }

    [Fact(DisplayName =
        "Unsupported stored ContentType fails safely (404, no bytes served)")]
    public async Task AdminUnsupportedContentType_FailsSafely()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        await SetExtractionPropertyAsync(extractionId, row =>
            row.ContentType = "text/html");

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();

        // The 404 is the JSON error contract, never the file bytes.
        Assert.NotEqual(PdfBytes, bytes);

        var envelope =
            await ReadJsonAsync<ErrorEnvelope>(response);
        Assert.NotNull(envelope?.Error);
        Assert.Equal("EXTRACTION_NOT_FOUND", envelope.Error.Code);
    }

    // ------------------------------------------------------------------
    // Agent API
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent can download its own extraction's file (200, exact bytes)")]
    public async Task AgentCanDownloadOwnExtractionFile()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        var response = await DownloadAsync(
            string.Format(AgentFileUrl, extractionId),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await EnsureOk(response, HttpStatusCode.OK);
        Assert.Equal(
            "application/pdf",
            response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(PdfBytes, bytes);
    }

    [Fact(DisplayName =
        "Another agent cannot download the extraction's file (404)")]
    public async Task AgentOtherAgentCannotDownload_Returns404()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        var response = await DownloadAsync(
            string.Format(AgentFileUrl, extractionId),
            TestDataSeeder.Ids.AgentB,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Missing extraction returns 404")]
    public async Task AgentMissingExtraction_Returns404()
    {
        var response = await DownloadAsync(
            string.Format(AgentFileUrl, TestDataSeeder.Ids.Unknown),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Soft-deleted extraction returns 404")]
    public async Task AgentDeletedExtraction_Returns404()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await SetExtractionPropertyAsync(extractionId, row =>
        {
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
        });

        var response = await DownloadAsync(
            string.Format(AgentFileUrl, extractionId),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Legacy NULL CreatedBy extraction cannot be downloaded by an agent (404)")]
    public async Task AgentNullCreatedByCannotDownload_Returns404()
    {
        // Admin-created extractions have no CreatedBy, mirroring the legacy
        // rows that predate the ownership column.
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        await WithScopeDbContextAsync(db =>
        {
            Assert.Null(db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId)
                .CreatedBy);
        });

        var response = await DownloadAsync(
            string.Format(AgentFileUrl, extractionId),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Out-of-scope extraction (other company's agent) cannot be downloaded (404)")]
    public async Task AgentOutOfScopeCannotDownload_Returns404()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        var response = await DownloadAsync(
            string.Format(AgentFileUrl, extractionId),
            TestDataSeeder.Ids.AgentB,
            "Agent",
            TestDataSeeder.Ids.CompanyB.ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Security
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Client-supplied filename cannot influence the storage path")]
    public async Task ClientFilenameCannotInfluenceStoragePath()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin",
            fileName: "../../escape-me.pdf");

        string? reference = null;
        await WithScopeDbContextAsync(db =>
        {
            var row = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId);
            reference = row.FileUrl;
        });

        // The stored reference is a GUID-based server path, not derived from
        // the malicious client filename.
        Assert.NotNull(reference);
        Assert.False(Path.IsPathRooted(reference));
        Assert.StartsWith("extractions/", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("escape", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("..", reference, StringComparison.Ordinal);

        var fullPath = Resolve(reference);
        Assert.True(File.Exists(fullPath));

        // The file outside the root must not exist.
        var escaped = Resolve("../../escape-me.pdf");
        Assert.False(File.Exists(escaped));

        var response = await DownloadAsync(
            string.Format(AdminFileUrl, extractionId),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);
        Assert.Equal(
            PdfBytes,
            await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName =
        "Tampered FileUrl traversal cannot escape the storage root")]
    public async Task FileUrlTraversalCannotEscapeStorageRoot()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        // Tamper the persisted reference with a traversal that escapes the
        // root and points at a decoy the API must never serve.
        const string traversal = "../../outside-decoy.pdf";

        var decoyPath = Path.Combine(
            Path.GetDirectoryName(FactoryStorageRoot)!,
            "outside-decoy.pdf");

        if (File.Exists(decoyPath))
        {
            File.Delete(decoyPath);
        }

        try
        {
            File.WriteAllBytes(decoyPath, [0x41, 0x42, 0x43]);

            await SetExtractionPropertyAsync(extractionId, row =>
                row.FileUrl = traversal);

            var response = await DownloadAsync(
                string.Format(AdminFileUrl, extractionId),
                TestDataSeeder.Ids.Admin,
                "Admin");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // The decoy is untouched: the traversal never escaped the root.
            Assert.Equal(
                [0x41, 0x42, 0x43],
                File.ReadAllBytes(decoyPath));

            var body = await response.Content.ReadAsByteArrayAsync();

            // The 404 is the JSON error contract, never the decoy bytes.
            Assert.NotEqual([0x41, 0x42, 0x43], body);

            var envelope =
                await ReadJsonAsync<ErrorEnvelope>(response);
            Assert.NotNull(envelope?.Error);
            Assert.Equal("EXTRACTION_NOT_FOUND", envelope.Error.Code);
        }
        finally
        {
            if (File.Exists(decoyPath))
            {
                File.Delete(decoyPath);
            }
        }
    }

    // ------------------------------------------------------------------
    // Response envelopes
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

    private sealed class ErrorEnvelope
    {
        public bool Success { get; set; }

        public ErrorPayload? Error { get; set; }
    }

    private sealed class ErrorPayload
    {
        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}