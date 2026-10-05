using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.AI;
using PROManagementSystem.Infrastructure.AI.DocumentExtractionService;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7C.1: OpenRouter provider configuration foundation. These tests prove
/// the options class binds from the "Extraction:OpenRouter" section with safe
/// non-secret defaults, that the API key can be supplied purely through
/// configuration (never hard-coded), and that OpenRouter configuration does
/// not disturb the existing Mock/Local providers. No network calls are made
/// and no real API key is used.
/// </summary>
public class OpenRouterOptionsConfigurationTests
{
    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "OpenRouterOptions defaults are safe, non-secret and unopinionated")]
    public void OpenRouterOptions_Defaults_AreCorrect()
    {
        var options = new OpenRouterOptions();

        Assert.Equal(string.Empty, options.ApiKey);
        Assert.Equal(string.Empty, options.Endpoint);
        Assert.Equal(string.Empty, options.Model);
        Assert.Equal(2048, options.MaxTokens);
        Assert.Equal(60, options.RequestTimeoutSeconds);
        Assert.Equal(2, options.MaxRetries);
        Assert.Equal(20_000, options.MaxInputCharacters);
        Assert.Equal(1_048_576, options.MaxResponseBytes);
    }

    [Fact(DisplayName =
        "A missing Extraction:OpenRouter section keeps the safe defaults")]
    public void MissingSection_KeepsDefaults()
    {
        var config = new ConfigurationBuilder().Build();

        var bound =
            config.GetSection("Extraction:OpenRouter")
                .Get<OpenRouterOptions>();

        var fromConfig = bound ?? new OpenRouterOptions();

        Assert.Equal(string.Empty, fromConfig.ApiKey);
        Assert.Equal(string.Empty, fromConfig.Endpoint);
        Assert.Equal(string.Empty, fromConfig.Model);
        Assert.Equal(2048, fromConfig.MaxTokens);
        Assert.Equal(60, fromConfig.RequestTimeoutSeconds);
        Assert.Equal(2, fromConfig.MaxRetries);
        Assert.Equal(20_000, fromConfig.MaxInputCharacters);
        Assert.Equal(1_048_576, fromConfig.MaxResponseBytes);
    }

    // ------------------------------------------------------------------
    // Binding
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "OpenRouterOptions binds all keys from the Extraction:OpenRouter section")]
    public void OpenRouterOptions_Binds_FromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extraction:OpenRouter:ApiKey"] = "test-only-key-not-a-real-secret",
                ["Extraction:OpenRouter:Endpoint"] = "https://openrouter.example.test/v1",
                ["Extraction:OpenRouter:Model"] = "test-model",
                ["Extraction:OpenRouter:MaxTokens"] = "1024",
                ["Extraction:OpenRouter:RequestTimeoutSeconds"] = "45",
                ["Extraction:OpenRouter:MaxRetries"] = "3",
                ["Extraction:OpenRouter:MaxInputCharacters"] = "8000",
                ["Extraction:OpenRouter:MaxResponseBytes"] = "2048"
            })
            .Build();

        var bound =
            config.GetSection("Extraction:OpenRouter")
                .Get<OpenRouterOptions>()!;

        Assert.Equal("test-only-key-not-a-real-secret", bound.ApiKey);
        Assert.Equal("https://openrouter.example.test/v1", bound.Endpoint);
        Assert.Equal("test-model", bound.Model);
        Assert.Equal(1024, bound.MaxTokens);
        Assert.Equal(45, bound.RequestTimeoutSeconds);
        Assert.Equal(3, bound.MaxRetries);
        Assert.Equal(8_000, bound.MaxInputCharacters);
        Assert.Equal(2_048, bound.MaxResponseBytes);
    }

    [Fact(DisplayName =
        "The API key is supplied via configuration only and never hard-coded")]
    public void ApiKey_ComesFromConfigurationOnly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extraction:OpenRouter:ApiKey"] = "configured-key-value"
            })
            .Build();

        var bound =
            config.GetSection("Extraction:OpenRouter")
                .Get<OpenRouterOptions>()!;

        // The value flows from configuration into the options; the production
        // class initializes ApiKey to empty and carries no embedded secret.
        Assert.Equal("configured-key-value", bound.ApiKey);
        Assert.Equal(string.Empty, new OpenRouterOptions().ApiKey);
    }

    // ------------------------------------------------------------------
    // Provider isolation
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "OpenRouter configuration does not change the underlying OCR provider")]
    [InlineData("Mock")]
    [InlineData("Local")]
    public void OpenRouterKeys_DoNotChangeOcrProvider(string provider)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extraction:Provider"] = provider,
                ["Extraction:OpenRouter:Endpoint"] = "https://openrouter.example.test/v1",
                ["Extraction:OpenRouter:Model"] = "test-model",
                ["Extraction:OpenRouter:MaxTokens"] = "512",
                ["Extraction:OpenRouter:ApiKey"] = "configured-key-value"
            })
            .Build();

        var bound =
            config.GetSection("Extraction")
                .Get<OcrOptions>()!;

        Assert.Equal(provider, bound.Provider);
    }
}

/// <summary>
/// Boot-level verification for Task 7C.1: with OpenRouter configuration keys
/// injected, the resolved extraction provider still follows only
/// "Extraction:Provider", and the application starts with Provider = Mock and
/// no API key. No network calls are made.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class OpenRouterProviderConfigurationTests : IntegrationTestBase
{
    public OpenRouterProviderConfigurationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static IReadOnlyDictionary<string, string?> OpenRouterOverride =>
        new Dictionary<string, string?>
        {
            ["Extraction:OpenRouter:Endpoint"] = "https://openrouter.example.test/v1",
            ["Extraction:OpenRouter:Model"] = "test-model",
            ["Extraction:OpenRouter:MaxTokens"] = "512",
            ["Extraction:OpenRouter:RequestTimeoutSeconds"] = "30",
            ["Extraction:OpenRouter:MaxRetries"] = "1",
            ["Extraction:OpenRouter:ApiKey"] = "test-only-key-not-a-real-secret"
        };

    [Fact(DisplayName =
        "The app boots with Provider = Mock and no API key, options resolve clean")]
    public void DefaultFactory_Boots_WithMock_AndNoApiKey()
    {
        using var scope = Factory.Services.CreateScope();

        var swipe = scope.ServiceProvider
            .GetRequiredService<IOptions<OcrOptions>>();
        Assert.Equal(OcrOptions.ProviderMock, swipe.Value.Provider);

        var openRouter = scope.ServiceProvider
            .GetRequiredService<IOptions<OpenRouterOptions>>();
        Assert.Equal(string.Empty, openRouter.Value.ApiKey);
        Assert.Equal(2048, openRouter.Value.MaxTokens);
        Assert.Equal(60, openRouter.Value.RequestTimeoutSeconds);

        var service = scope.ServiceProvider
            .GetRequiredService<IDocumentExtractionService>();
        Assert.IsType<MockDocumentExtractionService>(service);
    }

    [Fact(DisplayName =
        "OpenRouter keys do not affect Mock provider resolution")]
    public void OpenRouterKeys_DoNotAffectMockResolution()
    {
        Assert.Equal(
            typeof(MockDocumentExtractionService),
            ResolveExtractorType("Mock"));
    }

    [Fact(DisplayName =
        "OpenRouter keys do not affect Local provider resolution")]
    public void OpenRouterKeys_DoNotAffectLocalResolution()
    {
        Assert.Equal(
            typeof(LocalDocumentExtractionService),
            ResolveExtractorType("Local"));
    }

    private Type ResolveExtractorType(string provider)
    {
        using var factory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var overrides =
                    new Dictionary<string, string?>(OpenRouterOverride)
                    {
                        ["Extraction:Provider"] = provider
                    };

                config.AddInMemoryCollection(overrides);
            }));

        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider
            .GetRequiredService<IDocumentExtractionService>()
            .GetType();
    }
}