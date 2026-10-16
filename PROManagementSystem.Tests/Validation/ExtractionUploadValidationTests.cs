using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.FileValidation;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Verifies the Task 1 hardening: both Admin and Agent document extraction
/// endpoints reject uploads that violate the shared <see cref="FileUploadPolicy"/>
/// (MIME allow-list, extension allow-list, size bounds, and the requirement
/// that the content type and extension correspond) with the standard 400
/// VALIDATION_ERROR envelope produced by FluentValidation + ValidationBehaviour.
/// The controller-level empty-file guard (FILE_REQUIRED) is left untouched and
/// its behavior is asserted here as well. Uploaded bodies carry the real magic
/// bytes for the declared content type so the Task 6B.2 signature check stays
/// out of the picture for these metadata-focused cases.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExtractionUploadValidationTests : IntegrationTestBase
{
    private const string AdminExtractUrl =
        "/api/v1/admin/documents/extract";

    private const string AgentExtractUrl =
        "/api/v1/agent/documents/extract";

    public ExtractionUploadValidationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string AgentToken(Guid userId)
        => JwtHelper.MintToken(
            userId,
            "Agent",
            userId == TestDataSeeder.Ids.AgentB
                ? "agent-b@test.local"
                : "agent-a@test.local",
            userId == TestDataSeeder.Ids.AgentB ? "Agent B" : "Agent A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

    private static string AdminToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.Admin,
            "Admin",
            "admin@test.local",
            "System Admin");

    private async Task<HttpResponseMessage> UploadAsync(
        string url,
        string token,
        string fileName,
        string contentType,
        long byteCount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var file = new ByteArrayContent(
            ContentBytes(contentType, byteCount));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        form.Add(new StringContent("Passport"), "documentType");
        request.Content = form;

        return await AuthClient.SendAsync(request);
    }

    private static byte[] ContentBytes(
        string contentType,
        long byteCount)
    {
        if (byteCount <= 0)
        {
            return [];
        }

        var bytes = new byte[byteCount];
        var signature = SignatureFor(contentType);
        var copyLength = (int)Math.Min(signature.Length, bytes.Length);
        Array.Copy(signature, 0, bytes, 0, copyLength);
        return bytes;
    }

    private static byte[] SignatureFor(string contentType)
        => contentType switch
        {
            "application/pdf" =>
                [0x25, 0x50, 0x44, 0x46, 0x2d],   // "%PDF-"
            "image/jpeg" => [0xff, 0xd8, 0xff],    // "FF D8 FF"
            "image/png" =>
                [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
            _ => []
        };

    private static async Task AssertValidation400Async(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
    }

    private static async Task AssertFileRequired400Async(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("FILE_REQUIRED", body);
    }

    private async Task AssertRejectedAsync(
        string url,
        string token,
        string fileName,
        string contentType,
        long byteCount)
        => await AssertValidation400Async(
            await UploadAsync(
                url,
                token,
                fileName,
                contentType,
                byteCount));

    private async Task AssertAcceptedAsync(
        string url,
        string token,
        string fileName,
        string contentType,
        long byteCount = 16)
        => Assert.Equal(
            HttpStatusCode.OK,
            (await UploadAsync(
                url,
                token,
                fileName,
                contentType,
                byteCount)).StatusCode);

    // ------------------------------------------------------------------
    // Admin: rejection cases
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: unsupported content type is a 400 VALIDATION_ERROR")]
    public async Task Admin_UnsupportedContentType_Returns400()
        => await AssertRejectedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.pdf",
            "text/plain",
            4);

    [Fact(DisplayName =
        "Admin: unsupported extension is a 400 VALIDATION_ERROR")]
    public async Task Admin_UnsupportedExtension_Returns400()
        => await AssertRejectedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.txt",
            "application/pdf",
            4);

    [Fact(DisplayName =
        "Admin: application/pdf with .jpg extension is a 400 VALIDATION_ERROR")]
    public async Task Admin_PdfMimeWithJpgExtension_Returns400()
        => await AssertRejectedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.jpg",
            "application/pdf",
            4);

    [Fact(DisplayName =
        "Admin: image/jpeg with .pdf extension is a 400 VALIDATION_ERROR")]
    public async Task Admin_JpegMimeWithPdfExtension_Returns400()
        => await AssertRejectedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.pdf",
            "image/jpeg",
            4);

    [Fact(DisplayName =
        "Admin: file over 20 MiB is a 400 VALIDATION_ERROR")]
    public async Task Admin_FileSizeOverLimit_Returns400()
        => await AssertRejectedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.pdf",
            "application/pdf",
            FileUploadPolicy.MaxFileSizeBytes + 1);

    [Fact(DisplayName =
        "Admin: empty file keeps the controller FILE_REQUIRED guard")]
    public async Task Admin_EmptyFile_ReturnsFileRequired()
        => await AssertFileRequired400Async(
            await UploadAsync(
                AdminExtractUrl,
                AdminToken(),
                "document.pdf",
                "application/pdf",
                0));

    // ------------------------------------------------------------------
    // Admin: accepted cases
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: application/pdf + .pdf extracts successfully")]
    public async Task Admin_ValidPdf_Returns200()
        => await AssertAcceptedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.pdf",
            "application/pdf");

    [Fact(DisplayName =
        "Admin: image/jpeg + .jpg extracts successfully")]
    public async Task Admin_ValidJpegJpg_Returns200()
        => await AssertAcceptedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.jpg",
            "image/jpeg");

    [Fact(DisplayName =
        "Admin: image/jpeg + .jpeg extracts successfully")]
    public async Task Admin_ValidJpegJpeg_Returns200()
        => await AssertAcceptedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.jpeg",
            "image/jpeg");

    [Fact(DisplayName =
        "Admin: image/png + .png extracts successfully")]
    public async Task Admin_ValidPng_Returns200()
        => await AssertAcceptedAsync(
            AdminExtractUrl,
            AdminToken(),
            "document.png",
            "image/png");

    // ------------------------------------------------------------------
    // Admin: case-insensitive extension acceptance
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "Admin: uppercase extension is accepted for a matching content type")]
    [InlineData("DOCUMENT.PDF", "application/pdf")]
    [InlineData("DOCUMENT.JPG", "image/jpeg")]
    [InlineData("DOCUMENT.PNG", "image/png")]
    public async Task Admin_UpperCaseExtension_IsAccepted(
        string fileName,
        string contentType)
        => await AssertAcceptedAsync(
            AdminExtractUrl,
            AdminToken(),
            fileName,
            contentType);

    // ------------------------------------------------------------------
    // Agent: rejection cases
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: unsupported content type is a 400 VALIDATION_ERROR")]
    public async Task Agent_UnsupportedContentType_Returns400()
        => await AssertRejectedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.pdf",
            "text/plain",
            4);

    [Fact(DisplayName =
        "Agent: unsupported extension is a 400 VALIDATION_ERROR")]
    public async Task Agent_UnsupportedExtension_Returns400()
        => await AssertRejectedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.txt",
            "application/pdf",
            4);

    [Fact(DisplayName =
        "Agent: image/png with .jpg extension is a 400 VALIDATION_ERROR")]
    public async Task Agent_PngMimeWithJpgExtension_Returns400()
        => await AssertRejectedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.jpg",
            "image/png",
            4);

    [Fact(DisplayName =
        "Agent: image/png with .pdf extension is a 400 VALIDATION_ERROR")]
    public async Task Agent_PngMimeWithPdfExtension_Returns400()
        => await AssertRejectedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.pdf",
            "image/png",
            4);

    [Fact(DisplayName =
        "Agent: file over 20 MiB is a 400 VALIDATION_ERROR")]
    public async Task Agent_FileSizeOverLimit_Returns400()
        => await AssertRejectedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.pdf",
            "application/pdf",
            FileUploadPolicy.MaxFileSizeBytes + 1);

    [Fact(DisplayName =
        "Agent: empty file keeps the controller FILE_REQUIRED guard")]
    public async Task Agent_EmptyFile_ReturnsFileRequired()
        => await AssertFileRequired400Async(
            await UploadAsync(
                AgentExtractUrl,
                AgentToken(TestDataSeeder.Ids.AgentA),
                "document.pdf",
                "application/pdf",
                0));

    // ------------------------------------------------------------------
    // Agent: accepted cases + ownership preservation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: application/pdf + .pdf extracts successfully")]
    public async Task Agent_ValidPdf_Returns200()
        => await AssertAcceptedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.pdf",
            "application/pdf");

    [Fact(DisplayName =
        "Agent: image/jpeg + .jpg extracts successfully")]
    public async Task Agent_ValidJpegJpg_Returns200()
        => await AssertAcceptedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.jpg",
            "image/jpeg");

    [Fact(DisplayName =
        "Agent: image/jpeg + .jpeg extracts successfully")]
    public async Task Agent_ValidJpegJpeg_Returns200()
        => await AssertAcceptedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.jpeg",
            "image/jpeg");

    [Fact(DisplayName =
        "Agent: image/png + .png extracts successfully")]
    public async Task Agent_ValidPng_Returns200()
        => await AssertAcceptedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.png",
            "image/png");

    [Fact(DisplayName =
        "Agent: a valid extract still persists CreatedBy")]
    public async Task Agent_ValidUpload_PreservesCreatedBy()
    {
        var response = await UploadAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            "document.pdf",
            "application/pdf",
            16);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WithScopeDbContextAsync(db =>
        {
            var row = db.DocumentExtractions
                .AsNoTracking()
                .SingleOrDefault(x => x.CreatedBy == TestDataSeeder.Ids.AgentA);
            Assert.NotNull(row);
        });
    }

    // ------------------------------------------------------------------
    // Agent: case-insensitive extension acceptance
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "Agent: uppercase extension is accepted for a matching content type")]
    [InlineData("DOCUMENT.PDF", "application/pdf")]
    [InlineData("DOCUMENT.JPG", "image/jpeg")]
    [InlineData("DOCUMENT.PNG", "image/png")]
    public async Task Agent_UpperCaseExtension_IsAccepted(
        string fileName,
        string contentType)
        => await AssertAcceptedAsync(
            AgentExtractUrl,
            AgentToken(TestDataSeeder.Ids.AgentA),
            fileName,
            contentType);
}