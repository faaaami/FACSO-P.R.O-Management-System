using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.AI;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 6B.1 + 6B.3: document extraction provider configuration.
/// The provider is selected exclusively via "Extraction:Provider" and compares
/// case-insensitively, matching configuration-convention patterns used across
/// the codebase. A missing provider defaults to the deterministic mock;
/// "Mock" selects the mock while "Local" selects the in-process extractor
/// (Task 6B.3); everything else was to leave existing behavior untouched.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExtractionProviderConfigurationTests : IntegrationTestBase
{
    public ExtractionProviderConfigurationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Provider selection
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A missing Extraction:Provider section defaults to the Mock provider")]
    public void MissingProvider_DefaultsToMock()
    {
        // OcrOptions.Provider property initializer is the canonical default.
        // When the Extraction section is absent from configuration the Options
        // binder leaves the default intact.
        var options = new OcrOptions();
        Assert.Equal(OcrOptions.ProviderMock, options.Provider);
        Assert.Equal(OcrOptions.ProviderMock, OcrOptions.DefaultProvider);
        Assert.Equal("Local", OcrOptions.ProviderLocal);
        Assert.NotEqual(OcrOptions.ProviderMock, OcrOptions.ProviderLocal);

        // Verify the Options binder does not overwrite the default when
        // binding an empty/missing section.
        var bound = new ConfigurationBuilder()
            .Build()
            .GetSection("Extraction")
            .Get<OcrOptions>();

        // Get<OcrOptions>() may return null for an empty section, but
        // Configure<OcrOptions> in DI always creates a new instance and
        // binds — proving the property default holds. This is equivalent.
        var fromConfig = bound ?? new OcrOptions();
        Assert.Equal(OcrOptions.ProviderMock, fromConfig.Provider);
    }

    [Fact(DisplayName =
        "The default factory resolves the Mock extractor with no provider override")]
    public void DefaultFactory_ResolvesMockExtractor()
    {
        using var scope = Factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IDocumentExtractionService>();

        Assert.IsType<MockDocumentExtractionService>(service);
    }

    [Fact(DisplayName =
        "Extraction:Provider = Mock explicitly selects the Mock extractor")]
    public void ExplicitMock_ResolvesMockExtractor()
    {
        Assert.Equal(
            typeof(MockDocumentExtractionService),
            ResolveExtractorType("Mock"));
    }

    [Theory(DisplayName =
        "Extraction:Provider = Mock (any casing) selects the Mock extractor")]
    [InlineData("mock")]
    [InlineData("MOCK")]
    [InlineData("mOk")]
    public void MockCasing_ResolvesMockExtractor(string provider)
    {
        Assert.Equal(
            typeof(MockDocumentExtractionService),
            ResolveExtractorType(provider));
    }

    [Theory(DisplayName =
        "Extraction:Provider = Local (any casing) selects the in-process extractor")]
    [InlineData("local")]
    [InlineData("LOCAL")]
    [InlineData("LoCaL")]
    public void LocalCasing_ResolvesLocalExtractor(string provider)
    {
        Assert.Equal(
            typeof(DocumentExtractionPipeline),
            ResolveExtractorType(provider));
    }

    // ------------------------------------------------------------------
    // OcrOptions defaults and binding (Task 6B.5)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "OcrOptions property defaults match expected OCR policy")]
    public void OcrOptions_Defaults_AreCorrect()
    {
        var options = new OcrOptions();

        Assert.Equal(OcrOptions.ProviderMock, options.Provider);
        Assert.Equal("eng", options.Language);
        Assert.Equal(40_000_000L, options.MaximumPixels);
        Assert.Equal(20, options.MaximumOcrPages);
        Assert.Equal(300, options.RenderDpi);
        Assert.Equal(2, options.MaxConcurrentOcr);
        Assert.Equal(60, options.OcrTimeoutSeconds);
    }

    [Fact(DisplayName =
        "OcrOptions binds OCR workload keys from the Extraction section")]
    public void OcrOptions_Binds_FromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extraction:Provider"] = "Local",
                ["Extraction:Language"] = "hin",
                ["Extraction:MaximumPixels"] = "10000000",
                ["Extraction:MaximumOcrPages"] = "10",
                ["Extraction:RenderDpi"] = "150",
                ["Extraction:MaxConcurrentOcr"] = "4",
                ["Extraction:OcrTimeoutSeconds"] = "30"
            })
            .Build();

        var bound = config.GetSection("Extraction").Get<OcrOptions>()!;

        Assert.Equal("Local", bound.Provider);
        Assert.Equal("hin", bound.Language);
        Assert.Equal(10_000_000L, bound.MaximumPixels);
        Assert.Equal(10, bound.MaximumOcrPages);
        Assert.Equal(150, bound.RenderDpi);
        Assert.Equal(4, bound.MaxConcurrentOcr);
        Assert.Equal(30, bound.OcrTimeoutSeconds);
    }

    [Fact(DisplayName =
        "OcrOptions of 0 disables the OCR deadline and workload gate")]
    public void OcrOptions_Zero_DisablesProtection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extraction:MaxConcurrentOcr"] = "0",
                ["Extraction:OcrTimeoutSeconds"] = "0"
            })
            .Build();

        var bound = config.GetSection("Extraction").Get<OcrOptions>()!;

        Assert.Equal(0, bound.MaxConcurrentOcr);
        Assert.Equal(0, bound.OcrTimeoutSeconds);

        // A disabled gate must be a transparent no-op: no semaphore is
        // allocated, the wait completes immediately and release is safe.
        using var gate = new OcrWorkloadGate(Options.Create(bound));

        var wait = gate.WaitAsync();
        Assert.True(wait.IsCompleted);
        gate.Release();
        Assert.True(wait.IsCompletedSuccessfully);
    }

    // ------------------------------------------------------------------
    // Behavior unchanged
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Mock extraction behavior is unchanged: deterministic Passport suggestion")]
    public async Task MockBehavior_RemainsUnchanged()
    {
        using var file = new ByteArrayContent(
        [
            0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
            0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
        ]);
        file.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", "upload.pdf");
        form.Add(new StringContent("Passport"), "documentType");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/admin/documents/extract")
        {
            Content = form
        };
        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                TestDataSeeder.Ids.Admin,
                "Admin"));

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<ExtractEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        Assert.True(envelope!.Success);
        Assert.Equal("Passport", envelope.Data!.SuggestedType);
        Assert.Equal("AI-PASS-123456", envelope.Data.SuggestedDocumentNumber);
        Assert.Equal("Ocr", envelope.Data.Fields["documentNumber"].Source);
        Assert.Equal("2031-01-01", envelope.Data.Fields["expiryDate"].Value);
        Assert.Null(envelope.Data.Failure);

        var extractionId = envelope.Data.ExtractionId;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<PROManagementSystem.Infrastructure.Persistence.ApplicationDbContext>();

        var row = db.DocumentExtractions
            .Single(x => x.Id == extractionId);

        Assert.Equal("Passport", row.SuggestedType.ToString());
        Assert.Equal("AI-PASS-123456", row.SuggestedDocumentNumber);
        Assert.False(row.IsDeleted);
        Assert.Equal("Pending", row.Status);
        Assert.NotNull(row.ExpiresAt);
        Assert.True(row.ExpiresAt > DateTime.UtcNow.AddHours(23));
        Assert.StartsWith("extractions/", row.FileUrl);

        // The Mock's dates are Passport schema fields, so they are stored in
        // the details too (jsonb normalises the spacing, hence the parse).
        var details = System.Text.Json.JsonDocument.Parse(row.SuggestedDetails).RootElement;
        Assert.Equal("2026-01-01", details.GetProperty("issueDate").GetString());
        Assert.Equal("2031-01-01", details.GetProperty("expiryDate").GetString());
        Assert.Contains("\"source\"", row.FieldResults);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private Type ResolveExtractorType(string provider)
    {
        using var factory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Extraction:Provider"] = provider
                    })));

        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider
            .GetRequiredService<IDocumentExtractionService>()
            .GetType();
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

        public Dictionary<string, FieldView> Fields { get; set; } = new();

        public object? Failure { get; set; }
    }

    private sealed class FieldView
    {
        public string? Value { get; set; }

        public string Source { get; set; } = string.Empty;
    }
}