using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Storage;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// End to end through the API with the Local provider (real Magick.NET image
/// preparation and real Tesseract OCR): staff choose the document type, the
/// MRZ is read for passports and Emirates IDs, other types fall back to
/// labelled text, and every outcome is a Pending draft with per-field
/// results and, when needed, a clear failure message. AI stays disabled.
/// All documents are synthetic (ICAO specimens or made-up values).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class LocalExtractionProviderIntegrationTests
    : IntegrationTestBase, IDisposable
{
    private const string AdminExtractUrl = "/api/v1/admin/documents/extract";
    private const string AgentExtractUrl = "/api/v1/agent/documents/extract";

    private static readonly string[] PassportHeader =
    {
        "UTOPIA  PASSPORT",
        "Surname  ERIKSSON",
        "Given names  ANNA MARIA"
    };

    private static readonly string[] PassportMrz =
    {
        MrzParserTests.Td3Line1,
        MrzParserTests.Td3Line2
    };

    private static readonly string[] EidBackMrz =
    {
        MrzParserTests.EidLine1,
        MrzParserTests.EidLine2,
        MrzParserTests.EidLine3
    };

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
    // Passport and Emirates ID: MRZ
    // ------------------------------------------------------------------

    // Tesseract's general English model reads the fixture's Courier New "L"
    // as "1" (and "<" as "K"); ICAO check digits cannot detect L/1, so the
    // read looks valid. Real MRZs use OCR-B. Re-enable once the MRZ passes
    // use an OCR-B/MRZ-trained model (Phase A follow-up) and the fixture uses
    // an OCR-B font or a real sample.
    private const string AwaitingOcrBModel =
        "Awaiting the OCR-B/MRZ Tesseract model: the general English model misreads " +
        "the Courier fixture's L as 1, which check digits cannot detect.";

    [Fact(Skip = AwaitingOcrBModel, DisplayName = "Passport photo: fields are read from the MRZ with check digits verified")]
    public async Task PassportPhoto_ReadsMrz()
    {
        var data = await ExtractAsync(
            "Passport", "passport.png", "image/png",
            TestMrzImageBuilder.Png(PassportHeader, PassportMrz));

        AssertField(data, "documentNumber", "L898902C3", "Mrz");
        AssertField(data, "passportNumber", "L898902C3", "Mrz");
        AssertField(data, "dateOfBirth", "1974-08-12", "Mrz");
        AssertField(data, "surname", "ERIKSSON", "Mrz");
        AssertField(data, "givenNames", "ANNA MARIA", "Mrz");
        AssertField(data, "nationality", "UTO", "Mrz");

        // The specimen expired in 2012: read correctly, flagged for review.
        var expiry = AssertField(data, "expiryDate", "2012-04-15", "Mrz");
        Assert.Contains("This document has expired.", Strings(expiry["warnings"]));

        Assert.Equal("L898902C3", data["suggestedDocumentNumber"]!.GetValue<string>());
        Assert.Null(data["failure"]);
        Assert.Null(data["confidence"]);
    }

    [Fact(Skip = AwaitingOcrBModel, DisplayName = "Passport photo taken upside down (no EXIF) is still read")]
    public async Task UpsideDownPassport_IsRead()
    {
        var data = await ExtractAsync(
            "Passport", "upside-down.jpg", "image/jpeg",
            TestMrzImageBuilder.Jpeg(PassportHeader, PassportMrz, rotateDegrees: 180));

        AssertField(data, "documentNumber", "L898902C3", "Mrz");
        Assert.Null(data["failure"]);
    }

    [Fact(DisplayName = "Passport PDF with a text layer: MRZ read from the text")]
    public async Task PassportPdfTextLayer_ReadsMrz()
    {
        var data = await ExtractAsync(
            "Passport", "passport.pdf", "application/pdf",
            TestPdfBuilder.CreateTextPdf(PassportHeader.Concat(PassportMrz).ToArray()));

        var number = AssertField(data, "documentNumber", "L898902C3", "Mrz");

        // Never presented as fully verified: check digits miss some look-alikes.
        Assert.Contains(DocumentExtractionPipeline.PassportReviewNote, Strings(number["warnings"]));
        AssertField(data, "dateOfBirth", "1974-08-12", "Mrz");
        Assert.Null(data["failure"]);
    }

    [Fact(DisplayName = "Emirates ID back: 784 number and card number read from the MRZ")]
    public async Task EmiratesIdBack_ReadsMrz()
    {
        var data = await ExtractAsync(
            "EmiratesId", "eid-back.png", "image/png",
            TestMrzImageBuilder.Png(new[] { "UNITED ARAB EMIRATES" }, EidBackMrz));

        AssertField(data, "idNumber", "784-1990-1234567-6", "Mrz");
        AssertField(data, "documentNumber", "784-1990-1234567-6", "Mrz");
        AssertField(data, "cardNumber", "123456789", "Mrz");
        AssertField(data, "dateOfBirth", "1990-01-15", "Mrz");
        AssertField(data, "expiryDate", "2031-02-20", "Mrz");
        AssertField(data, "nationality", "PAK", "Mrz");
        Assert.Null(data["failure"]);
    }

    [Fact(DisplayName = "Passport without an MRZ: clear 'MRZ not found' message, nothing invented")]
    public async Task PassportWithoutMrz_ExplainsFailure()
    {
        var data = await ExtractAsync(
            "Passport", "no-mrz.png", "image/png",
            TestMrzImageBuilder.Png(PassportHeader, Array.Empty<string>()));

        Assert.Equal(ExtractionFailures.MrzNotFound, data["failure"]!["code"]!.GetValue<string>());
        Assert.Contains("passport photo page", data["failure"]!["message"]!.GetValue<string>());
        Assert.Null(data["suggestedDocumentNumber"]);
    }

    [Fact(DisplayName = "Emirates ID front without MRZ: 784 number from OCR, back-side hint shown")]
    public async Task EmiratesIdFront_FallsBackToOcr()
    {
        var data = await ExtractAsync(
            "EmiratesId", "eid-front.png", "image/png",
            TestMrzImageBuilder.Png(
                new[] { "Resident Identity Card", "ID Number", "784-1990-1234567-6", "Name: Mohammed Ali Khan" },
                Array.Empty<string>()));

        AssertField(data, "idNumber", "784-1990-1234567-6", "Ocr");
        Assert.Equal(ExtractionFailures.MrzNotFound, data["failure"]!["code"]!.GetValue<string>());
        Assert.Contains("back side", data["failure"]!["message"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------
    // Other types (AI disabled)
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Visa PDF: labelled values are read from the text layer")]
    public async Task VisaPdf_ReadsLabelledValues()
    {
        var data = await ExtractAsync(
            "Visa", "visa.pdf", "application/pdf",
            TestPdfBuilder.CreateTextPdf(TestDocumentFixtures.VisaLines()));

        var visaNumber = Assert.IsType<JsonObject>(data["fields"]!["visaNumber"]);
        Assert.Equal("Ocr", visaNumber["source"]!.GetValue<string>());
        Assert.Equal(
            visaNumber["value"]!.GetValue<string>(),
            data["suggestedDocumentNumber"]!.GetValue<string>());
        Assert.Null(data["failure"]);
    }

    [Fact(DisplayName = "Unlabelled text with AI off: 'automatic reading not enabled' message")]
    public async Task UnlabelledText_AiOff_SaysDisabled()
    {
        var data = await ExtractAsync(
            "TradeLicense", "licence.pdf", "application/pdf",
            TestPdfBuilder.CreateTextPdf("Commercial licence for general trading", "Dubai Economy"));

        Assert.Equal(ExtractionFailures.AiDisabled, data["failure"]!["code"]!.GetValue<string>());
        Assert.Empty(Assert.IsType<JsonObject>(data["fields"]));
    }

    [Fact(DisplayName = "A blank image: 'no text could be read' message")]
    public async Task BlankImage_SaysNothingRead()
    {
        var data = await ExtractAsync(
            "Visa", "blank.png", "image/png",
            TestImageBuilder.CreateBlankPng());

        var code = data["failure"]!["code"]!.GetValue<string>();
        Assert.Contains(code, new[] { ExtractionFailures.NothingRead, ExtractionFailures.ImageTooSmall });
    }

    [Fact(DisplayName = "An undecodable image: 'could not be read' message, never a 500")]
    public async Task UndecodableImage_SaysUnreadable()
    {
        // A valid PNG signature followed by garbage.
        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }
            .Concat(Enumerable.Repeat((byte)0x42, 512))
            .ToArray();

        var data = await ExtractAsync("Passport", "broken.png", "image/png", bytes);

        Assert.Equal(ExtractionFailures.ImageUnreadable, data["failure"]!["code"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------
    // Contract, storage, privacy
    // ------------------------------------------------------------------

    [Theory(DisplayName = "The document type is required before upload")]
    [InlineData(AdminExtractUrl, null)]
    [InlineData(AdminExtractUrl, "Spaceship")]
    [InlineData(AdminExtractUrl, "42")]
    [InlineData(AgentExtractUrl, null)]
    public async Task DocumentType_IsRequired(string url, string? documentType)
    {
        var response = await UploadAsync(
            url,
            url == AgentExtractUrl ? AgentToken() : AdminToken(),
            documentType,
            "passport.png",
            "image/png",
            TestImageBuilder.CreateBlankPng());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("DOCUMENT_TYPE_REQUIRED", body["error"]!["code"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Field results and the failure are stored on the draft")]
    public async Task Draft_StoresFieldResultsAndFailure()
    {
        var data = await ExtractAsync(
            "EmiratesId", "eid-back.png", "image/png",
            TestMrzImageBuilder.Png(new[] { "UNITED ARAB EMIRATES" }, EidBackMrz));

        var row = await RowAsync(data["extractionId"]!.GetValue<Guid>());

        Assert.Equal("Pending", row.Status);
        Assert.Equal(DocumentType.EmiratesId, row.SuggestedType);
        Assert.Equal("784-1990-1234567-6", row.SuggestedDocumentNumber);
        Assert.Null(row.FailureCode);
        Assert.Contains("784-1990-1234567-6", row.SuggestedDetails);

        // Stored per-field results round-trip with value and source.
        var stored = JsonNode.Parse(row.FieldResults)!;
        Assert.Equal("784-1990-1234567-6", stored["idNumber"]!["value"]!.GetValue<string>());
        Assert.Equal("Mrz", stored["idNumber"]!["source"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Agent drafts record the agent as creator")]
    public async Task Agent_Draft_RecordsCreator()
    {
        var response = await UploadAsync(
            AgentExtractUrl, AgentToken(), "Passport", "passport.png", "image/png",
            TestMrzImageBuilder.Png(PassportHeader, PassportMrz));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!;

        var row = await RowAsync(data["extractionId"]!.GetValue<Guid>());
        Assert.Equal(TestDataSeeder.Ids.AgentA, row.CreatedBy);
    }

    [Fact(DisplayName = "Recognised text is never returned, and the audit has no file name")]
    public async Task Text_IsNeverReturned_AndAuditHasNoFileName()
    {
        const string marker = "FASCO-SECRET-7C9D2B";

        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(), "Visa", "Ahmed_Khan_visa.pdf", "application/pdf",
            TestPdfBuilder.CreateTextPdf("Visa", marker));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(marker, body);

        var id = JsonNode.Parse(body)!["data"]!["extractionId"]!.GetValue<Guid>();

        await using var db = await CreateScopeDbContextAsync();
        var audit = await db.AuditLogs.SingleAsync(x => x.EntityId == id);
        Assert.DoesNotContain("Ahmed_Khan", audit.Description);
        Assert.DoesNotContain(marker, audit.Description);
    }

    [Fact(DisplayName = "An OCR deadline gives 'took too long' and keeps the stored file")]
    public async Task OcrDeadline_SaysTimeout_KeepsFile()
    {
        _localClient = BuildLocalFactoryClient(blockOcr: true);

        var data = await ExtractAsync(
            "Visa", "slow.png", "image/png",
            TestImageBuilder.CreateTextPng("SLOW"));

        Assert.Equal(ExtractionFailures.OcrTimeout, data["failure"]!["code"]!.GetValue<string>());
        await AssertStoredFileExistsAsync(data["extractionId"]!.GetValue<Guid>());
    }

    [Fact(DisplayName = "Upload validation still rejects fake PDF bytes")]
    public async Task FakePdf_IsRejected()
    {
        var response = await UploadAsync(
            AdminExtractUrl, AdminToken(), "Passport", "fake.pdf", "application/pdf",
            "not really a pdf"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static JsonObject AssertField(JsonNode data, string key, string value, string source)
    {
        var fields = Assert.IsType<JsonObject>(data["fields"]);
        Assert.True(fields.ContainsKey(key), $"Field '{key}' was not returned.");

        var field = Assert.IsType<JsonObject>(fields[key]);
        Assert.Equal(value, field["value"]?.GetValue<string>());
        Assert.Equal(source, field["source"]!.GetValue<string>());
        Assert.Empty(Strings(field["errors"]));

        return field;
    }

    private static List<string> Strings(JsonNode? array)
        => Assert.IsType<JsonArray>(array).Select(x => x!.GetValue<string>()).ToList();

    private async Task<JsonNode> ExtractAsync(string documentType, string fileName, string contentType, byte[] content)
    {
        var response = await UploadAsync(AdminExtractUrl, AdminToken(), documentType, fileName, contentType, content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {body}");

        return JsonNode.Parse(body)!["data"]!;
    }

    private async Task<HttpResponseMessage> UploadAsync(
        string url,
        string token,
        string? documentType,
        string fileName,
        string contentType,
        byte[] content)
    {
        var client = _localClient ??= BuildLocalFactoryClient(blockOcr: false);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);

        if (documentType is not null)
        {
            form.Add(new StringContent(documentType), "documentType");
        }

        request.Content = form;
        return await client.SendAsync(request);
    }

    private async Task<Domain.Entities.DocumentExtraction> RowAsync(Guid extractionId)
    {
        await using var db = await CreateScopeDbContextAsync();
        return await db.DocumentExtractions.AsNoTracking().SingleAsync(x => x.Id == extractionId);
    }

    private async Task AssertStoredFileExistsAsync(Guid extractionId)
    {
        var row = await RowAsync(extractionId);
        Assert.NotNull(row.FileUrl);

        using var scope = _localFactory!.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
        var fullPath = Path.Combine(storage.RootPath, row.FileUrl!.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(fullPath), $"Expected stored extraction file at {fullPath}");
    }

    private static string AdminToken()
        => JwtHelper.MintToken(TestDataSeeder.Ids.Admin, "Admin", "admin@test.local", "System Admin");

    private static string AgentToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.AgentA, "Agent", "agent-a@test.local", "Agent A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

    private HttpClient BuildLocalFactoryClient(bool blockOcr)
    {
        _localClient?.Dispose();
        _localFactory?.Dispose();

        _localFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Extraction:Provider"] = OcrOptions.ProviderLocal,
                    ["Extraction:Ai:Enabled"] = "false",
                    ["Extraction:OcrTimeoutSeconds"] = blockOcr ? "1" : "60"
                }));

            if (blockOcr)
            {
                builder.ConfigureServices(services =>
                    services.AddScoped<IImageOcrExtractor, BlockingOcrExtractor>());
            }
        });

        return _localFactory.CreateClient();
    }

    /// <summary>Never returns until the server-side OCR deadline cancels it.</summary>
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
}
