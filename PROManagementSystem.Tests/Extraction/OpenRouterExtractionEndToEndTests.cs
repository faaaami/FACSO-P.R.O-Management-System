using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using PROManagementSystem.Infrastructure.AI.DocumentExtractionService;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Storage;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7C.3.3: end-to-end integration of the OpenRouter extraction provider
/// through the REAL extract / confirm / GET / file API endpoints. A per-test
/// host selects Extraction:Provider = OpenRouter and installs a
/// <see cref="RecordingHttpMessageHandler"/> as the primary handler for ONLY
/// the named OpenRouter client, so the full pipeline runs — file validation,
/// storage, text recovery, classification, prompt building, the HTTP call, the
/// structured parser and persistence — without any real network call.
///
/// These tests prove the provider is wired correctly end to end; they are
/// intentionally test-only and change no production behavior.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class OpenRouterExtractionEndToEndTests
    : IntegrationTestBase, IDisposable
{
    private const string AdminExtractUrl =
        "/api/v1/admin/documents/extract";

    private const string AgentExtractUrl =
        "/api/v1/agent/documents/extract";

    private const string FakeEndpoint =
        "https://openrouter.example.test/v1";

    private const string FakeModel = "test-model";

    private const int FakeMaxTokens = 2048;

    private const string FakeApiKey =
        "test-only-key-not-a-real-secret";

    private const string PassportNumber = "P123456";

    private static readonly DateTime IssueDateUtc =
        new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ExpiryDateUtc =
        new(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Logical extraction the fake model returns. unknownField MUST be removed
    // by the parser's schema filtering; it is a deterministic sentinel.
    private const string LogicalExtraction =
        "{\"documentType\":\"Passport\"," +
        "\"documentNumber\":\"" + PassportNumber + "\"," +
        "\"issueDate\":\"2025-01-01\"," +
        "\"expiryDate\":\"2035-01-01\"," +
        "\"ownerType\":\"Employee\"," +
        "\"details\":{\"passportNumber\":\"" + PassportNumber + "\"," +
        "\"nationality\":\"Indian\"," +
        "\"unknownField\":\"REMOVE_ME\"}," +
        "\"confidence\":0.94}";

    private static string SuccessBody =>
        "{\"id\":\"gen-e2e\",\"choices\":[{\"message\":{\"content\":" +
        JsonSerializer.Serialize(LogicalExtraction) + "}}]}";

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public OpenRouterExtractionEndToEndTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    void IDisposable.Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    // ------------------------------------------------------------------
    // Success E2E
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: OpenRouter success E2E stores schema-filtered suggestions " +
        "as a Pending draft and never auto-confirms")]
    public async Task Admin_Success_PersistsSuggestions()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            SuccessBody);
        var client = BuildClient(handler);

        var content = PassportPdf();

        var response = await UploadAsync(
            client,
            AdminExtractUrl,
            AdminToken(),
            content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = JsonSerializer.Deserialize<
            Envelope<ExtractPayload>>(body, JsonOptions);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);

        var data = envelope.Data!;

        // --- API response matches the AI suggestions ------------------
        Assert.Equal("Passport", data.SuggestedType);
        Assert.Equal(PassportNumber, data.SuggestedDocumentNumber);
        Assert.Equal(IssueDateUtc.Date, data.SuggestedIssueDate!.Value.Date);
        Assert.Equal(ExpiryDateUtc.Date, data.SuggestedExpiryDate!.Value.Date);

        var responseDetails = ParseDetails(data.SuggestedDetails);

        Assert.Equal(
            PassportNumber,
            responseDetails.GetProperty("passportNumber").GetString());
        Assert.Equal(
            "Indian",
            responseDetails.GetProperty("nationality").GetString());
        Assert.False(responseDetails.TryGetProperty("unknownField", out _));

        // The raw model response is never echoed through the API.
        Assert.DoesNotContain(
            "choices",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gen-e2e", body, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeApiKey, body, StringComparison.Ordinal);

        AssertPersistedExtraction(data.ExtractionId, content);
        await AssertDraftRetrievalAsync(client, data.ExtractionId);
        await AssertFilePreviewAsync(client, data.ExtractionId, content);
    }

    // ------------------------------------------------------------------
    // Human override E2E
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: human confirmation overrides the AI suggestion while the " +
        "extraction keeps the AI's non-authoritative suggestion")]
    public async Task Admin_HumanOverride_Wins()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            SuccessBody);
        var client = BuildClient(handler);

        var content = PassportPdf();

        var response = await UploadAsync(
            client,
            AdminExtractUrl,
            AdminToken(),
            content);

        var envelope = JsonSerializer.Deserialize<
            Envelope<ExtractPayload>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);

        var extractionId = envelope!.Data!.ExtractionId;

        var confirmResponse = await ConfirmAsync(
            client,
            extractionId,
            AdminToken(),
            DocumentType.Visa,
            "V999999");

        Assert.Equal(HttpStatusCode.Created, confirmResponse.StatusCode);

        var confirmEnvelope = JsonSerializer.Deserialize<
            Envelope<ConfirmPayload>>(
            await confirmResponse.Content.ReadAsStringAsync(),
            JsonOptions);

        Assert.NotNull(confirmEnvelope?.Data);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var document = db.Documents
            .AsNoTracking()
            .Single(x => x.Id == confirmEnvelope!.Data!.DocumentId);

        // The human-supplied values are authoritative — not the AI's.
        Assert.Equal(DocumentType.Visa, document.Type);
        Assert.Equal("V999999", document.DocumentNumber);
        Assert.NotEqual(PassportNumber, document.DocumentNumber);

        var extraction = db.DocumentExtractions
            .AsNoTracking()
            .Single(x => x.Id == extractionId);

        Assert.Equal("Confirmed", extraction.Status);

        // The AI suggestion is preserved but remains non-authoritative.
        Assert.Equal(DocumentType.Passport, extraction.SuggestedType);
        Assert.Equal(PassportNumber, extraction.SuggestedDocumentNumber);

        // Confirmation keeps the stored file.
        Assert.True(File.Exists(ResolveStoredFile(extraction)));
    }

    // ------------------------------------------------------------------
    // Agent E2E
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: OpenRouter success E2E persists the caller as CreatedBy and " +
        "keeps the draft readable by that agent")]
    public async Task Agent_Success_PersistsCreatedBy()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            SuccessBody);
        var client = BuildClient(handler);

        var content = PassportPdf();

        var response = await UploadAsync(
            client,
            AgentExtractUrl,
            AgentToken(),
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = JsonSerializer.Deserialize<
            Envelope<ExtractPayload>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);

        var extractionId = envelope!.Data!.ExtractionId;

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var extraction = db.DocumentExtractions
            .AsNoTracking()
            .Single(x => x.Id == extractionId);

        Assert.Equal(TestDataSeeder.Ids.AgentA, extraction.CreatedBy);
        Assert.Equal("Pending", extraction.Status);
        Assert.Equal(DocumentType.Passport, extraction.SuggestedType);
        Assert.Equal(PassportNumber, extraction.SuggestedDocumentNumber);

        // The creating agent can read their own draft.
        var draftResponse = await GetAsync(
            client,
            $"/api/v1/agent/documents/extractions/{extractionId}",
            AgentToken());

        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
    }

    // ------------------------------------------------------------------
    // Outbound request assertions
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The outbound OpenRouter request matches the chat/completions " +
        "contract and carries no internal data")]
    public async Task Outbound_Request_Contract()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            SuccessBody);
        var client = BuildClient(handler);

        await UploadAsync(
            client,
            AdminExtractUrl,
            AdminToken(),
            PassportPdf());

        var request = Assert.Single(handler.Requests);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.NotNull(request.RequestUri);
        Assert.EndsWith(
            "/chat/completions",
            request.RequestUri!.ToString(),
            StringComparison.Ordinal);

        Assert.NotNull(request.Authorization);
        Assert.Equal("Bearer", request.Authorization!.Scheme);
        Assert.Equal(FakeApiKey, request.Authorization.Parameter);
        Assert.Equal("application/json", request.ContentType);

        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;

        Assert.Equal(FakeModel, root.GetProperty("model").GetString());
        Assert.Equal(FakeMaxTokens, root.GetProperty("max_tokens").GetInt32());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());

        var expectedSystemPrompt = new OpenRouterPromptBuilder(
                new DocumentTypeSchemaProvider())
            .Build("ignored", DocumentType.Passport)
            .SystemPrompt;

        Assert.Equal(
            expectedSystemPrompt,
            messages[0].GetProperty("content").GetString());

        var userPrompt = messages[1].GetProperty("content").GetString()!;

        Assert.Contains(
            "<document_text>",
            userPrompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "</document_text>",
            userPrompt,
            StringComparison.Ordinal);
        Assert.Contains("P-90321", userPrompt, StringComparison.Ordinal);

        // No internal identifiers, storage paths or credentials.
        string[] prohibited =
        {
            FakeApiKey,
            "extractions/",
            "postgres",
            "ConnectionStrings",
            "http://",
            "https://",
            TestDataSeeder.Ids.Admin.ToString(),
            TestDataSeeder.Ids.AgentA.ToString(),
            TestDataSeeder.Ids.EmployeeA.ToString(),
            TestDataSeeder.Ids.CompanyA.ToString(),
        };

        foreach (var token in prohibited)
        {
            Assert.DoesNotContain(
                token,
                request.Body!,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------
    // Task 7C.3.4: failure semantics E2E
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: a provider timeout degrades to a Pending draft, preserves " +
        "the stored file and never creates a Document")]
    public async Task Admin_ProviderTimeout_DegradesSafely()
    {
        var handler = new RecordingHttpMessageHandler(
            async (_, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var client = BuildClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Extraction:OpenRouter:RequestTimeoutSeconds"] = "1",
                ["Extraction:OpenRouter:MaxRetries"] = "0",
            });

        var content = PassportPdf();

        var response = await UploadAsync(
            client,
            AdminExtractUrl,
            AdminToken(),
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = JsonSerializer.Deserialize<
            Envelope<ExtractPayload>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);

        Assert.NotNull(envelope?.Data);
        Assert.Equal("Passport", envelope!.Data!.SuggestedType);
        Assert.NotNull(envelope.Data.Failure);

        var row = GetExtractionRow(envelope.Data.ExtractionId);

        Assert.Equal("Pending", row.Status);
        Assert.Equal(DocumentType.Passport, row.SuggestedType);
        Assert.NotNull(row.FailureCode);
        Assert.Null(row.SuggestedDocumentNumber);
        Assert.True(File.Exists(ResolveStoredFile(row)));
        AssertNoDocument(PassportNumber);
    }

    [Fact(DisplayName =
        "Admin: exhausted retries degrade to a Pending draft, preserve the " +
        "stored file, never create a Document, and manual confirmation works")]
    public async Task Admin_RetriesExhausted_DegradesSafely()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            "{}");

        var client = BuildClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Extraction:OpenRouter:MaxRetries"] = "2",
            });

        var content = PassportPdf();

        var response = await UploadAsync(
            client,
            AdminExtractUrl,
            AdminToken(),
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.RequestCount);

        var envelope = JsonSerializer.Deserialize<
            Envelope<ExtractPayload>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);

        var extractionId = envelope!.Data!.ExtractionId;
        var row = GetExtractionRow(extractionId);

        Assert.Equal("Pending", row.Status);
        Assert.Equal(DocumentType.Passport, row.SuggestedType);
        Assert.NotNull(row.FailureCode);
        Assert.Null(row.SuggestedDocumentNumber);
        Assert.True(File.Exists(ResolveStoredFile(row)));
        AssertNoDocument(PassportNumber);

        // Manual confirmation remains authoritative after a failed extraction.
        var confirmResponse = await ConfirmAsync(
            client,
            extractionId,
            AdminToken(),
            DocumentType.Visa,
            "V999999");

        Assert.Equal(HttpStatusCode.Created, confirmResponse.StatusCode);

        var confirmEnvelope = JsonSerializer.Deserialize<
            Envelope<ConfirmPayload>>(
            await confirmResponse.Content.ReadAsStringAsync(),
            JsonOptions);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var document = db.Documents
            .AsNoTracking()
            .Single(x => x.Id == confirmEnvelope!.Data!.DocumentId);

        Assert.Equal(DocumentType.Visa, document.Type);
        Assert.Equal("V999999", document.DocumentNumber);

        Assert.Equal(
            "Confirmed",
            db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId)
                .Status);
    }

    [Fact(DisplayName =
        "Admin: a malformed model response degrades to a Pending draft and " +
        "preserves the stored file")]
    public async Task Admin_MalformedResponse_DegradesSafely()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            "this is not valid json");

        var client = BuildClient(handler);
        var content = PassportPdf();

        var response = await UploadAsync(
            client,
            AdminExtractUrl,
            AdminToken(),
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = JsonSerializer.Deserialize<
            Envelope<ExtractPayload>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);

        var row = GetExtractionRow(envelope!.Data!.ExtractionId);

        Assert.Equal("Pending", row.Status);
        Assert.Equal(DocumentType.Passport, row.SuggestedType);
        Assert.NotNull(row.FailureCode);
        Assert.Null(row.SuggestedDocumentNumber);
        Assert.True(File.Exists(ResolveStoredFile(row)));
        AssertNoDocument(PassportNumber);
    }

    [Fact(DisplayName =
        "Caller cancellation propagates through the DI pipeline instead of " +
        "being converted into a neutral result")]
    public async Task CallerCancellation_Propagates()
    {
        var handler = new RecordingHttpMessageHandler(
            async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        BuildClient(handler);

        using var scope = _factory!.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<OpenRouterDocumentExtractionService>();

        using var cts = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExtractAsync(
                string.Join("\n", TestDocumentFixtures.PassportLines()),
                DocumentType.Passport,
                cts.Token));
    }

    // ------------------------------------------------------------------
    // Assertions
    // ------------------------------------------------------------------

    private DocumentExtraction GetExtractionRow(Guid extractionId)
    {
        using var scope = _factory!.Services.CreateScope();

        return scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .DocumentExtractions
            .AsNoTracking()
            .Single(x => x.Id == extractionId);
    }

    private void AssertNoDocument(string documentNumber)
    {
        using var scope = _factory!.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        Assert.Empty(
            db.Documents
                .AsNoTracking()
                .Where(x => x.EmployeeId == TestDataSeeder.Ids.EmployeeA
                    && x.DocumentNumber == documentNumber));
    }

    private void AssertPersistedExtraction(Guid extractionId, byte[] uploaded)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var row = db.DocumentExtractions
            .AsNoTracking()
            .Single(x => x.Id == extractionId);

        Assert.Equal("Pending", row.Status);
        Assert.Equal(DocumentType.Passport, row.SuggestedType);
        Assert.Equal(PassportNumber, row.SuggestedDocumentNumber);
        Assert.Equal(IssueDateUtc.Date, row.SuggestedIssueDate!.Value.Date);
        Assert.Equal(ExpiryDateUtc.Date, row.SuggestedExpiryDate!.Value.Date);

        Assert.NotNull(row.FileUrl);
        Assert.StartsWith("extractions/", row.FileUrl, StringComparison.Ordinal);
        Assert.Equal("passport.pdf", row.FileName);
        Assert.Equal("application/pdf", row.ContentType);
        Assert.Equal(uploaded.Length, row.FileSize);

        Assert.NotNull(row.ExpiresAt);
        Assert.InRange(
            row.ExpiresAt!.Value,
            DateTime.UtcNow.AddHours(23),
            DateTime.UtcNow.AddHours(25));

        var details = ParseDetails(row.SuggestedDetails);

        Assert.Equal(
            PassportNumber,
            details.GetProperty("passportNumber").GetString());
        Assert.Equal(
            "Indian",
            details.GetProperty("nationality").GetString());

        // unknownField is removed by schema filtering; the raw model response
        // is never stored verbatim.
        Assert.False(details.TryGetProperty("unknownField", out _));
        Assert.DoesNotContain(
            "choices",
            row.SuggestedDetails,
            StringComparison.OrdinalIgnoreCase);

        // No automatic confirmation: the AI never creates a Document.
        Assert.Empty(
            db.Documents
                .AsNoTracking()
                .Where(x => x.EmployeeId == TestDataSeeder.Ids.EmployeeA
                    && x.DocumentNumber == PassportNumber));

        // The stored physical file exists, is non-zero, and is byte-identical.
        var fullPath = ResolveStoredFile(row);

        Assert.True(File.Exists(fullPath));
        Assert.True(new FileInfo(fullPath).Length > 0);
        Assert.Equal(uploaded, File.ReadAllBytes(fullPath));
    }

    private async Task AssertDraftRetrievalAsync(
        HttpClient client,
        Guid extractionId)
    {
        var response = await GetAsync(
            client,
            $"/api/v1/admin/documents/extractions/{extractionId}",
            AdminToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = JsonSerializer.Deserialize<
            Envelope<ExtractPayload>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);

        var data = envelope!.Data!;

        Assert.Equal("Passport", data.SuggestedType);
        Assert.Equal(PassportNumber, data.SuggestedDocumentNumber);

        // D1: the review payload deliberately does not carry the internal
        // storage reference — the dedicated /file endpoint streams the bytes.
        // The upload really did persist one server-side (asserted above), so
        // this is information hygiene, not a missing file.
        Assert.Null(data.FileUrl);
        Assert.DoesNotContain(
            "extractions/",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        var details = ParseDetails(data.SuggestedDetails);

        Assert.Equal(
            PassportNumber,
            details.GetProperty("passportNumber").GetString());
        Assert.False(details.TryGetProperty("unknownField", out _));
    }

    private static async Task AssertFilePreviewAsync(
        HttpClient client,
        Guid extractionId,
        byte[] expected)
    {
        var response = await GetAsync(
            client,
            $"/api/v1/admin/documents/extractions/{extractionId}/file",
            AdminToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/pdf",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static byte[] PassportPdf()
        => TestPdfBuilder.CreateTextPdf(
            TestDocumentFixtures.PassportLines());

    private HttpClient BuildClient(
        RecordingHttpMessageHandler handler,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Extraction:Provider"] = "OpenRouter",
            ["Extraction:OpenRouter:Endpoint"] = FakeEndpoint,
            ["Extraction:OpenRouter:Model"] = FakeModel,
            ["Extraction:OpenRouter:MaxTokens"] =
                FakeMaxTokens.ToString(CultureInfo.InvariantCulture),
            ["Extraction:OpenRouter:RequestTimeoutSeconds"] =
                30.ToString(CultureInfo.InvariantCulture),
            ["Extraction:OpenRouter:MaxRetries"] =
                0.ToString(CultureInfo.InvariantCulture),
            ["Extraction:OpenRouter:ApiKey"] = FakeApiKey,
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                settings[pair.Key] = pair.Value;
            }
        }

        _factory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(settings));

            builder.ConfigureServices(services =>
                services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
                    new NamedClientHandlerFilter(
                        OpenRouterExtractionServiceExtensions.HttpClientName,
                        handler)));
        });

        return _client = _factory.CreateClient();
    }

    private string ResolveStoredFile(DocumentExtraction row)
    {
        using var scope = _factory!.Services.CreateScope();

        var root = scope.ServiceProvider
            .GetRequiredService<IOptions<StorageOptions>>()
            .Value
            .RootPath;

        return Path.Combine(
            root,
            row.FileUrl!.Replace('/', Path.DirectorySeparatorChar));
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string url,
        string token,
        byte[] content,
        string fileName = "passport.pdf",
        string contentType = "application/pdf")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        form.Add(new StringContent("Passport"), "documentType");
        request.Content = form;

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client,
        Guid extractionId,
        string token,
        DocumentType type,
        string documentNumber)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/documents/extractions/{extractionId}/confirm");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        request.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            employeeId = TestDataSeeder.Ids.EmployeeA,
            clientEntityId = (Guid?)null,
            type,
            documentNumber,
            details = new { visaNumber = documentNumber },
            issueDate = (DateTime?)null,
            expiryDate = (DateTime?)null,
            fileUrl = (string?)null,
            fileName = (string?)null,
            contentType = (string?)null,
            fileSize = (long?)null,
        });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string url,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request);
    }

    private static JsonElement ParseDetails(JsonElement details)
    {
        if (details.ValueKind == JsonValueKind.String)
        {
            using var document = JsonDocument.Parse(details.GetString()!);
            return document.RootElement.Clone();
        }

        return details;
    }

    private static JsonElement ParseDetails(string detailsJson)
    {
        using var document = JsonDocument.Parse(detailsJson);
        return document.RootElement.Clone();
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

    // ------------------------------------------------------------------
    // Envelopes
    // ------------------------------------------------------------------

    private sealed class Envelope<T>
    {
        public bool Success { get; set; }

        public T? Data { get; set; }
    }

    private sealed class ExtractPayload
    {
        public Guid ExtractionId { get; set; }

        public string? SuggestedType { get; set; }

        public string? SuggestedDocumentNumber { get; set; }

        public DateTime? SuggestedIssueDate { get; set; }

        public DateTime? SuggestedExpiryDate { get; set; }

        public string? SuggestedOwnerType { get; set; }

        public FailureView? Failure { get; set; }

        public JsonElement SuggestedDetails { get; set; }

        public string? FileUrl { get; set; }
    }

    private sealed class ConfirmPayload
    {
        public Guid DocumentId { get; set; }
    }

    /// <summary>
    /// Replaces the primary handler of ONLY the named OpenRouter client so the
    /// rest of the host's transports (if any) are untouched.
    /// </summary>
    private sealed class NamedClientHandlerFilter
        : IHttpMessageHandlerBuilderFilter
    {
        private readonly string _clientName;
        private readonly HttpMessageHandler _handler;

        public NamedClientHandlerFilter(
            string clientName,
            HttpMessageHandler handler)
        {
            _clientName = clientName;
            _handler = handler;
        }

        public Action<HttpMessageHandlerBuilder> Configure(
            Action<HttpMessageHandlerBuilder> next)
            => builder =>
            {
                next(builder);

                if (string.Equals(
                        builder.Name,
                        _clientName,
                        StringComparison.Ordinal))
                {
                    builder.PrimaryHandler = _handler;
                }
            };
    }

    private sealed class FailureView
    {
        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
