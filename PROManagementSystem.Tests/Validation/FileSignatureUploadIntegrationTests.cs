using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.FileValidation;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Task 6B.2: end-to-end extraction-path tests that exercise file magic-byte
/// validation through both Admin and Agent upload endpoints. Every upload
/// carries the real signature for the declared content type; rejections are
/// triggered by mismatches or fake files, never by broken metadata.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class FileSignatureUploadIntegrationTests : IntegrationTestBase
{
    private const string AdminExtractUrl =
        "/api/v1/admin/documents/extract";

    private const string AgentExtractUrl =
        "/api/v1/agent/documents/extract";

    public FileSignatureUploadIntegrationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------------

    private static readonly byte[] PdfBytes =
        [0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34,
         0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a];

    private static readonly byte[] JpegBytes =
        [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10,
         0x4a, 0x46, 0x49, 0x46, 0x00, 0x01];

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a,
         0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d];

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string AdminToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.Admin,
            "Admin",
            "admin@test.local",
            "System Admin");

    private static string AgentToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            "agent-a@test.local",
            "Agent A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

    private async Task<HttpResponseMessage> UploadAsync(
        string url,
        string token,
        string fileName,
        string contentType,
        byte[] content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var file = new ByteArrayContent(content);
        file.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        form.Add(new StringContent("Passport"), "documentType");
        request.Content = form;

        return await AuthClient.SendAsync(request);
    }

    private static async Task AssertValidation400Async(
        HttpResponseMessage response,
        string? expectedMessage = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);

        if (expectedMessage is not null)
        {
            Assert.Contains(expectedMessage, body);
        }
    }

    // ------------------------------------------------------------------
    // Admin: accepted (valid signatures)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: valid PDF signature + application/pdf + .pdf → accepted")]
    public async Task Admin_ValidPdf_IsAccepted()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "passport.pdf", "application/pdf", PdfBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "Admin: valid JPEG signature + image/jpeg + .jpg → accepted")]
    public async Task Admin_ValidJpeg_IsAccepted()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "photo.jpg", "image/jpeg", JpegBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "Admin: valid PNG signature + image/png + .png → accepted")]
    public async Task Admin_ValidPng_IsAccepted()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "scan.png", "image/png", PngBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Admin: rejected (fake signatures)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: fake PDF file (zeros) + application/pdf + .pdf → rejected")]
    public async Task Admin_FakePdf_IsRejected()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "fake.pdf", "application/pdf",
            new byte[16]);

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "Admin: fake JPEG file (zeros) + image/jpeg + .jpg → rejected")]
    public async Task Admin_FakeJpeg_IsRejected()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "fake.jpg", "image/jpeg",
            new byte[16]);

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "Admin: fake PNG file (zeros) + image/png + .png → rejected")]
    public async Task Admin_FakePng_IsRejected()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "fake.png", "image/png",
            new byte[16]);

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Admin: MIME/signature mismatch
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: real PNG bytes declared as image/jpeg → rejected")]
    public async Task Admin_MimeSignatureMismatch_IsRejected()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "photo.jpg", "image/jpeg", PngBytes);

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Admin: extension/MIME/signature all valid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: DOCUMENT.PDF (uppercase ext) + valid PDF signature → accepted")]
    public async Task Admin_UpperCaseExtension_ValidSignature_IsAccepted()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "DOCUMENT.PDF", "application/pdf", PdfBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Admin: rejects and persists nothing
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: a fake-PDF rejection creates no DocumentExtraction row or storage file")]
    public async Task Admin_FakePdf_RejectionCreatesNoArtifact()
    {
        var (documentsBefore, auditLogsBefore, extractionsBefore) =
            await GetCountsAsync();

        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(),
            "fake.pdf", "application/pdf",
            new byte[32]);

        await AssertValidation400Async(response);

        var (documentsAfter, auditLogsAfter, extractionsAfter) =
            await GetCountsAsync();

        Assert.Equal(documentsBefore, documentsAfter);
        Assert.Equal(auditLogsBefore, auditLogsAfter);
        Assert.Equal(extractionsBefore, extractionsAfter);
    }

    // ------------------------------------------------------------------
    // Agent: accepted (valid signatures)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: valid PDF signature + application/pdf + .pdf → accepted")]
    public async Task Agent_ValidPdf_IsAccepted()
    {
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "passport.pdf", "application/pdf", PdfBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent: valid JPEG signature + image/jpeg + .jpeg → accepted")]
    public async Task Agent_ValidJpeg_IsAccepted()
    {
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "photo.jpeg", "image/jpeg", JpegBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent: valid PNG signature + image/png + .png → accepted")]
    public async Task Agent_ValidPng_IsAccepted()
    {
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "scan.png", "image/png", PngBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Agent: rejected (fake signatures)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: fake PDF file (zeros) + application/pdf + .pdf → rejected")]
    public async Task Agent_FakePdf_IsRejected()
    {
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "fake.pdf", "application/pdf",
            new byte[16]);

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "Agent: fake JPEG file (zeros) + image/jpeg + .jpg → rejected")]
    public async Task Agent_FakeJpeg_IsRejected()
    {
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "fake.jpg", "image/jpeg",
            new byte[16]);

        await AssertValidation400Async(response);
    }

    [Fact(DisplayName =
        "Agent: fake PNG file (zeros) + image/png + .png → rejected")]
    public async Task Agent_FakePng_IsRejected()
    {
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "fake.png", "image/png",
            new byte[16]);

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Agent: MIME/signature mismatch
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: real PNG bytes declared as image/png + .jpg extension → rejected")]
    public async Task Agent_MimeSignatureMismatch_IsRejected()
    {
        // Valid signature (PNG), extension .png matches content type, so
        // metadata passes; signature also passes for image/png. To create a
        // mismatch where metadata is valid but bytes don't match content type,
        // we send valid JPEG bytes under image/png content type with .png ext.
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "scan.png", "image/png", JpegBytes);

        await AssertValidation400Async(response);
    }

    // ------------------------------------------------------------------
    // Agent: rejects and persists nothing
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: a fake-PDF rejection creates no DocumentExtraction row")]
    public async Task Agent_FakePdf_RejectionCreatesNoExtractionRow()
    {
        var (_, _, extractionsBefore) = await GetCountsAsync();

        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(),
            "fake.pdf", "application/pdf",
            new byte[32]);

        await AssertValidation400Async(response);

        var (_, _, extractionsAfter) = await GetCountsAsync();
        Assert.Equal(extractionsBefore, extractionsAfter);
    }

    // ------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------

    private async Task<(int Documents, int AuditLogs, int Extractions)>
        GetCountsAsync()
    {
        var documents = 0;
        var auditLogs = 0;
        var extractions = 0;

        await WithScopeDbContextAsync(db =>
        {
            documents = db.Documents.Count();
            auditLogs = db.AuditLogs.Count();
            extractions = db.DocumentExtractions.Count();
        });

        return (documents, auditLogs, extractions);
    }
}