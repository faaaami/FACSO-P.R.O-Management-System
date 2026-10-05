using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Infrastructure.Storage;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 6B.3/6B.4: end-to-end extraction-path tests for the "Local" provider
/// (Extraction:Provider = Local). Selectable PDFs are parsed for their text
/// layer and JPEG/PNG images are OCR'd, but the API stays neutral; scanned-
/// style, blank and malformed inputs produce a pending draft instead of a 500;
/// and extracted text is never exposed in the response. The default provider
/// remains the Mock (asserted in ExtractionProviderConfigurationTests).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class LocalExtractionProviderIntegrationTests
    : IntegrationTestBase, IDisposable
{
    private const string AdminExtractUrl =
        "/api/v1/admin/documents/extract";

    private const string AgentExtractUrl =
        "/api/v1/agent/documents/extract";

    private const string SecretMarker = "FASCO-SECRET-7C9D2B";

    private WebApplicationFactory<Program>? _localFactory;
    private HttpClient? _localClient;

    public LocalExtractionProviderIntegrationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    void IDisposable.Dispose()
    {
        _localClient?.Dispose();

        _localFactory?.Dispose();
    }

    // ------------------------------------------------------------------
    // Local provider: accepted PDFs
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: Local + selectable PDF creates a neutral Pending draft and hides the text layer")]
    public async Task Admin_Local_SelectablePdf_CreatesNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(SecretMarker));

        var envelope = AssertBodyNeutralPending(envelopeBody: body);

        // The extracted text must never leak into the API response.
        Assert.DoesNotContain(SecretMarker, body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Null(row.SuggestedType);
        Assert.Null(row.SuggestedDocumentNumber);
        Assert.Null(row.SuggestedIssueDate);
        Assert.Null(row.SuggestedExpiryDate);
        Assert.Null(row.SuggestedOwnerType);
        Assert.Equal(0m, row.Confidence);
        Assert.Equal("Pending", row.Status);
        Assert.False(row.IsDeleted);
        Assert.NotNull(row.ExpiresAt);
        Assert.True(row.ExpiresAt > DateTime.UtcNow.AddHours(23));
        Assert.StartsWith("extractions/", row.FileUrl);

        // Task 7B.5: an unclassified extraction persists the canonical empty
        // object, never null.
        Assert.Equal("{}", row.SuggestedDetails);
    }

    [Fact(DisplayName =
        "Admin: Local + scanned-style PDF still creates a Pending draft, never 500")]
    public async Task Admin_Local_ScannedStylePdf_ReturnsNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "scan.pdf",
            "application/pdf",
            TestPdfBuilder.CreateScannedStylePdf());

        var envelope = AssertBodyNeutralPending(body);
    }

    [Fact(DisplayName =
        "Admin: Local + unreadable PDF bytes still create a draft, never 500")]
    public async Task Admin_Local_MalformedPdf_ReturnsNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "broken.pdf",
            "application/pdf",
            TestPdfBuilder.CreateMalformedPdf());

        AssertBodyNeutralPending(body);
    }

    [Fact(DisplayName =
        "Agent: Local + selectable PDF persists the caller as CreatedBy")]
    public async Task Agent_Local_SelectablePdf_PreservesCreatedBy()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AgentExtractUrl,
            AgentToken(),
            "passport.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(SecretMarker));

        var envelope = AssertBodyNeutralPending(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal(TestDataSeeder.Ids.AgentA, row.CreatedBy);
        Assert.Equal("Pending", row.Status);
    }

    // ------------------------------------------------------------------
    // Local provider: scanned PDF OCR (Task 6B.5)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: Local + single-page scanned PDF creates a neutral Pending draft")]
    public async Task Admin_Local_ScannedPdf_CreatesNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "scan.pdf",
            "application/pdf",
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(SecretMarker)));

        var envelope = AssertBodyNeutralPending(envelopeBody: body);

        Assert.DoesNotContain(SecretMarker, body);
        Assert.NotEqual(Guid.Empty, envelope.Data!.ExtractionId);
    }

    [Fact(DisplayName =
        "Admin: Local + multi-page scanned PDF creates a neutral Pending draft")]
    public async Task Admin_Local_MultiPageScannedPdf_CreatesNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "scan-multi.pdf",
            "application/pdf",
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(SecretMarker),
                TestImageBuilder.CreateBlankJpeg(),
                TestImageBuilder.CreateTextJpeg(SecretMarker)));

        AssertBodyNeutralPending(body);
    }

    [Fact(DisplayName =
        "Agent: Local + scanned PDF persists the caller as CreatedBy")]
    public async Task Agent_Local_ScannedPdf_PreservesCreatedBy()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AgentExtractUrl,
            AgentToken(),
            "scan.pdf",
            "application/pdf",
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(SecretMarker)));

        var envelope = AssertBodyNeutralPending(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal(TestDataSeeder.Ids.AgentA, row.CreatedBy);
        Assert.Equal("Pending", row.Status);
    }

    [Fact(DisplayName =
        "Admin: Local + blank scanned PDF creates a neutral Pending draft")]
    public async Task Admin_Local_BlankScannedPdf_ReturnsNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "blank-scan.pdf",
            "application/pdf",
            TestPdfBuilder.CreateBlankScannedPdf());

        AssertBodyNeutralPending(body);
    }

    [Fact(DisplayName =
        "Admin: Local + over-limit scanned PDF is skipped before rendering")]
    public async Task Admin_Local_OverPageLimitScannedPdf_CreatesNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "over-limit.pdf",
            "application/pdf",
            TestPdfBuilder.CreateOverPageLimitScannedPdf(21));

        AssertBodyNeutralPending(body);
    }

    [Fact(DisplayName =
        "Stored file remains on disk when scanned-PDF OCR fails soft (no cleanup)")]
    public async Task Local_ScannedPdf_OcrFailure_KeepsStoredFile()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "scan.pdf",
            "application/pdf",
            TestPdfBuilder.CreateBlankScannedPdf());

        AssertBodyNeutralPending(body);

        await AssertStoredFileExistsAsync(body);
    }

    [Fact(DisplayName =
        "Stored file remains on disk when the document is malformed")]
    public async Task Local_MalformedPdf_KeepsStoredFile()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "broken.pdf",
            "application/pdf",
            TestPdfBuilder.CreateMalformedPdf());

        AssertBodyNeutralPending(body);

        await AssertStoredFileExistsAsync(body);
    }

    [Fact(DisplayName =
        "Stored file remains on disk when a corrupt image OCRs to nothing")]
    public async Task Local_CorruptImage_KeepsStoredFile()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "broken.png",
            "image/png",
            TestImageBuilder.CreateTruncatedPng());

        AssertBodyNeutralPending(body);

        await AssertStoredFileExistsAsync(body);
    }

    [Fact(DisplayName =
        "Admin: Local + OCR deadline keeps the stored file and returns a neutral Pending draft")]
    public async Task Admin_Local_OcrTimeout_KeepsStoredFile_ReturnsNeutralDraft()
    {
        _localClient = BuildLocalTimeoutFactoryClient();

        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "scan.png",
            "image/png",
            TestImageBuilder.CreateTextPng(SecretMarker));

        var envelope = AssertBodyNeutralPending(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal(0m, row.Confidence);
        Assert.Null(row.SuggestedType);
        Assert.Equal("Pending", row.Status);
        Assert.False(row.IsDeleted);

        await AssertStoredFileExistsAsync(body);
    }

    // ------------------------------------------------------------------
    // Local provider: image OCR (Task 6B.4)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: Local + text JPEG is OCR'd to a neutral Pending draft and hides the text")]
    public async Task Admin_Local_TextJpeg_CreatesNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "scan.jpg",
            "image/jpeg",
            TestImageBuilder.CreateTextJpeg(SecretMarker));

        var envelope = AssertBodyNeutralPending(body);

        Assert.DoesNotContain(SecretMarker, body);
        Assert.NotEqual(Guid.Empty, envelope.Data!.ExtractionId);
    }

    [Fact(DisplayName =
        "Admin: Local + text PNG is OCR'd to a neutral Pending draft and hides the text")]
    public async Task Admin_Local_TextPng_CreatesNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "scan.png",
            "image/png",
            TestImageBuilder.CreateTextPng(SecretMarker));

        var envelope = AssertBodyNeutralPending(body);

        Assert.DoesNotContain(SecretMarker, body);
        Assert.NotEqual(Guid.Empty, envelope.Data!.ExtractionId);
    }

    [Fact(DisplayName =
        "Admin: Local + blank image still creates a Pending draft, never 500")]
    public async Task Admin_Local_BlankImage_ReturnsNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "blank.png",
            "image/png",
            TestImageBuilder.CreateBlankPng());

        AssertBodyNeutralPending(body);
    }

    [Fact(DisplayName =
        "Agent: Local + text PNG persists the caller as CreatedBy")]
    public async Task Agent_Local_TextPng_PreservesCreatedBy()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AgentExtractUrl,
            AgentToken(),
            "scan.png",
            "image/png",
            TestImageBuilder.CreateTextPng(SecretMarker));

        var envelope = AssertBodyNeutralPending(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal(TestDataSeeder.Ids.AgentA, row.CreatedBy);
        Assert.Equal("Pending", row.Status);
    }

    // ------------------------------------------------------------------
    // Local provider: validation still applies
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: Local still rejects fake PDF bytes (magic-byte validation intact)")]
    public async Task Admin_Local_FakePdf_IsRejected()
    {
        var response = await LocalUploadAsync(
            AdminExtractUrl,
            AdminToken(),
            "fake.pdf",
            "application/pdf",
            new byte[16]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
    }

    [Fact(DisplayName =
        "Admin: Local still rejects unsupported content types")]
    public async Task Admin_Local_UnsupportedContentType_IsRejected()
    {
        var response = await LocalUploadAsync(
            AdminExtractUrl,
            AdminToken(),
            "note.txt",
            "text/plain",
            TestPdfBuilder.CreateTextPdf(SecretMarker));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
        Assert.DoesNotContain(SecretMarker, body);
    }

    // ------------------------------------------------------------------
    // Local provider: document classification (Task 6C)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: Local + synthetic Passport PDF classifies as Passport")]
    public async Task Admin_Local_PassportPdf_ClassifiesAsPassport()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport-fixture.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.PassportLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.NotNull(envelope?.Data);
        Assert.True(envelope!.Success);
        Assert.Equal("Passport", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
        Assert.Equal("Pending", await GetStatusAsync(envelope.Data.ExtractionId));

        Assert.DoesNotContain(
            TestDocumentFixtures.SecretMarker,
            body);
    }

    [Fact(DisplayName =
        "Admin: Local + synthetic Visa PDF classifies as Visa")]
    public async Task Admin_Local_VisaPdf_ClassifiesAsVisa()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "visa-fixture.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.VisaLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("Visa", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
    }

    [Fact(DisplayName =
        "Admin: Local + synthetic Emirates ID PDF classifies as EmiratesId")]
    public async Task Admin_Local_EmiratesIdPdf_ClassifiesAsEmiratesId()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "emirates-id-fixture.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.EmiratesIdLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("EmiratesId", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
    }

    [Fact(DisplayName =
        "Admin: Local + synthetic Labor Card PDF classifies as LaborCard")]
    public async Task Admin_Local_LaborCardPdf_ClassifiesAsLaborCard()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "labor-card-fixture.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.LaborCardLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("LaborCard", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
    }

    [Fact(DisplayName =
        "Admin: Local + synthetic Trade License PDF classifies as TradeLicense")]
    public async Task Admin_Local_TradeLicensePdf_ClassifiesAsTradeLicense()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "trade-license-fixture.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.TradeLicenseLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("TradeLicense", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
    }

    [Fact(DisplayName =
        "Admin: Local + synthetic Establishment Card PDF classifies as EstablishmentCard")]
    public async Task Admin_Local_EstablishmentCardPdf_ClassifiesAsEstablishmentCard()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "establishment-card-fixture.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.EstablishmentCardLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("EstablishmentCard", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
    }

    [Fact(DisplayName =
        "Admin: Local + unknown document text PDF returns neutral draft")]
    public async Task Admin_Local_UnknownDocumentPdf_ReturnsNeutralDraft()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "unknown-fixture.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.UnknownDocumentLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Null(envelope.Data!.SuggestedType);
        Assert.Equal(0m, envelope.Data.Confidence);
    }

    [Fact(DisplayName =
        "Classification suggestion is stored in DB for Passport fixture")]
    public async Task Admin_Local_PassportPdf_StoresClassificationInDb()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport-db.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.PassportLines()));

        var envelope = DeserializeExtractBody(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal(DocumentType.Passport, row.SuggestedType);
        Assert.True(row.Confidence > 0m);
        Assert.Equal("Pending", row.Status);
        Assert.False(row.IsDeleted);
        Assert.NotNull(row.ExpiresAt);
    }

    [Fact(DisplayName =
        "Agent: Local + synthetic Passport PDF persists the caller as CreatedBy")]
    public async Task Agent_Local_PassportPdf_PreservesCreatedBy()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AgentExtractUrl,
            AgentToken(),
            "passport-agent.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.PassportLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("Passport", envelope.Data!.SuggestedType);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal(TestDataSeeder.Ids.AgentA, row.CreatedBy);
        Assert.Equal("Pending", row.Status);
    }

    [Fact(DisplayName =
        "Image OCR classification: synthetic passport JPEG is classified")]
    public async Task Admin_Local_PassportJpeg_ImageOcrClassifies()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport-img.jpg",
            "image/jpeg",
            TestImageBuilder.CreateDocumentTextJpeg(
                TestDocumentFixtures.PassportImageText()));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("Passport", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
    }

    [Fact(DisplayName =
        "Scanned PDF classification: synthetic passport scanned PDF is classified")]
    public async Task Admin_Local_PassportScannedPdf_ClassifiesPassport()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport-scan.pdf",
            "application/pdf",
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateDocumentTextJpeg(
                    TestDocumentFixtures.PassportImageText())));

        var envelope = DeserializeExtractBody(body);

        Assert.Equal("Passport", envelope.Data!.SuggestedType);
        Assert.True(envelope.Data.Confidence > 0m);
    }

    // ------------------------------------------------------------------
    // Suggested details (Task 7B.5)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: Local + synthetic Passport PDF persists schema-defined suggested details")]
    public async Task Admin_Local_PassportPdf_PersistsSuggestedDetails()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport-details.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.PassportLines()));

        var envelope = DeserializeExtractBody(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal(DocumentType.Passport, row.SuggestedType);

        var stored = ReadDetailsObject(row.SuggestedDetails);

        Assert.Equal(3, stored.Count);
        Assert.Equal("P-90321", stored["passportNumber"].GetString());
        Assert.Equal("TESTLAND", stored["nationality"].GetString());
        Assert.Equal("1990-01-01", stored["dateOfBirth"].GetString());

        // Suggestion-only: the extracted text must not leak into the response.
        Assert.DoesNotContain(TestDocumentFixtures.SecretMarker, body);
    }

    [Fact(DisplayName =
        "Admin: Local + scanned passport PDF populates suggested details via OCR")]
    public async Task Admin_Local_ScannedPassportPdf_PopulatesSuggestedDetails()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport-details-scan.pdf",
            "application/pdf",
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateDocumentTextJpeg(
                    TestDocumentFixtures.PassportImageText())));

        var envelope = DeserializeExtractBody(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        // OCR-based build: the values may vary slightly with the rendering, so
        // prove the pipeline wiring (non-empty, schema-driven) rather than
        // byte-exact OCR output.
        var stored = ReadDetailsObject(row.SuggestedDetails);

        Assert.True(stored.Count > 0);
        Assert.Contains("passportNumber", stored.Keys);
        Assert.Contains("nationality", stored.Keys);
        Assert.Contains("dateOfBirth", stored.Keys);
        Assert.DoesNotContain(TestDocumentFixtures.SecretMarker, body);
    }

    [Fact(DisplayName =
        "Admin: Local + passport JPEG OCR populates suggested details")]
    public async Task Admin_Local_PassportJpeg_PopulatesSuggestedDetails()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "passport-details.jpg",
            "image/jpeg",
            TestImageBuilder.CreateDocumentTextJpeg(
                TestDocumentFixtures.PassportImageText()));

        var envelope = DeserializeExtractBody(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        var stored = ReadDetailsObject(row.SuggestedDetails);

        Assert.True(stored.Count > 0);
        Assert.Contains("passportNumber", stored.Keys);
        Assert.DoesNotContain(TestDocumentFixtures.SecretMarker, body);
    }

    [Fact(DisplayName =
        "Admin: Local + unknown document persists the canonical empty object")]
    public async Task Admin_Local_UnknownDocument_PersistsEmptySuggestedDetails()
    {
        var body = await LocalUploadAndReadBodyAsync(
            AdminExtractUrl,
            AdminToken(),
            "unknown-details.pdf",
            "application/pdf",
            TestPdfBuilder.CreateTextPdf(
                TestDocumentFixtures.UnknownDocumentLines()));

        var envelope = DeserializeExtractBody(body);

        Assert.Null(envelope.Data!.SuggestedType);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.Equal("{}", row.SuggestedDetails);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Dictionary<string, System.Text.Json.JsonElement>
        ReadDetailsObject(string? json)
    {
        Assert.NotNull(json);
        Assert.NotEqual("null", json);

        var details = System.Text.Json.JsonSerializer.Deserialize<
            Dictionary<string, System.Text.Json.JsonElement>>(json);

        Assert.NotNull(details);

        return details!;
    }

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

    private async Task<HttpResponseMessage> LocalUploadAsync(
        string url,
        string token,
        string fileName,
        string contentType,
        byte[] content)
    {
        var client = _localClient ??= BuildLocalFactoryClient();

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var file = new ByteArrayContent(content);
        file.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        request.Content = form;

        return await client.SendAsync(request);
    }

    private async Task<string> LocalUploadAndReadBodyAsync(
        string url,
        string token,
        string fileName,
        string contentType,
        byte[] content)
    {
        var response = await LocalUploadAsync(
            url, token, fileName, contentType, content);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    private HttpClient BuildLocalFactoryClient()
    {
        _localFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Extraction:Provider"] = OcrOptions.ProviderLocal
                    })));

        return _localFactory.CreateClient();
    }

    /// <summary>
    /// Local-provider host whose heavy OCR never completes on its own, so the
    /// server-side OCR deadline (1s) deterministically ends the operation with
    /// a neutral degradation instead of a 500. The stored physical file must
    /// survive that deadline.
    /// </summary>
    private HttpClient BuildLocalTimeoutFactoryClient()
    {
        _localFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Extraction:Provider"] = OcrOptions.ProviderLocal,
                        ["Extraction:OcrTimeoutSeconds"] = "1"
                    }));

            builder.ConfigureServices(services =>
                services.AddScoped<IImageOcrExtractor, BlockingOcrExtractor>());
        });

        return _localFactory.CreateClient();
    }

    /// <summary>
    /// Deterministic stand-in for the real Tesseract extractor: it never
    /// returns until its token is cancelled, which is exactly what the
    /// server-side OCR deadline does.
    /// </summary>
    private sealed class BlockingOcrExtractor : IImageOcrExtractor
    {
        public async Task<OcrTextExtractionResult> ExtractAsync(
            Stream imageStream,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new OcrTextExtractionResult(string.Empty, false);
        }
    }

    private async Task AssertStoredFileExistsAsync(string envelopeBody)
    {
        var envelope = System.Text.Json.JsonSerializer.Deserialize<ExtractEnvelope>(
            envelopeBody,
            JsonOptions);

        Assert.NotNull(envelope?.Data);

        using var db = await CreateScopeDbContextAsync();

        var row = db.DocumentExtractions
            .AsNoTracking()
            .Single(x => x.Id == envelope.Data!.ExtractionId);

        Assert.NotNull(row.FileUrl);

        using var scope = _localFactory!.Services.CreateScope();
        var storage = scope.ServiceProvider
            .GetRequiredService<IOptions<StorageOptions>>().Value;

        var fullPath = Path.Combine(
            storage.RootPath,
            row.FileUrl.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(
            File.Exists(fullPath),
            $"Expected stored extraction file at {fullPath}");
    }

    private static ExtractEnvelope AssertBodyNeutralPending(string envelopeBody)
    {
        var envelope = System.Text.Json.JsonSerializer.Deserialize<ExtractEnvelope>(
            envelopeBody,
            JsonOptions);

        Assert.NotNull(envelope?.Data);
        Assert.True(envelope!.Success);
        Assert.Null(envelope.Data!.SuggestedType);
        Assert.Null(envelope.Data.SuggestedDocumentNumber);
        Assert.Null(envelope.Data.SuggestedIssueDate);
        Assert.Null(envelope.Data.SuggestedExpiryDate);
        Assert.Null(envelope.Data.SuggestedOwnerType);
        Assert.Equal(0m, envelope.Data.Confidence);
        Assert.NotEqual(Guid.Empty, envelope.Data.ExtractionId);

        return envelope;
    }

    private static ExtractEnvelope DeserializeExtractBody(string envelopeBody)
    {
        var envelope = System.Text.Json.JsonSerializer.Deserialize<ExtractEnvelope>(
            envelopeBody,
            JsonOptions);

        Assert.NotNull(envelope?.Data);
        Assert.True(envelope!.Success);

        return envelope;
    }

    private async Task<string> GetStatusAsync(Guid extractionId)
    {
        using var db = await CreateScopeDbContextAsync();

        return db.DocumentExtractions
            .AsNoTracking()
            .Single(x => x.Id == extractionId)
            .Status;
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

        public string? SuggestedType { get; set; }

        public string? SuggestedDocumentNumber { get; set; }

        public DateTime? SuggestedIssueDate { get; set; }

        public DateTime? SuggestedExpiryDate { get; set; }

        public string? SuggestedOwnerType { get; set; }

        public decimal Confidence { get; set; }
    }
}