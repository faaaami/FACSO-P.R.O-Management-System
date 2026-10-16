using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Storage;

/// <summary>
/// Verifies the end-to-end local storage behavior: the extract endpoints
/// persist a server-generated reference onto the extraction and onto disk,
/// and the confirm endpoints treat that reference as authoritative so a
/// client-supplied fake FileUrl can never replace the stored file location.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class FileStorageIntegrationTests : IntegrationTestBase
{
    private const string AdminExtractUrl =
        "/api/v1/admin/documents/extract";

    private const string AgentExtractUrl =
        "/api/v1/agent/documents/extract";

    private static readonly byte[] PdfBytes =
    [
        0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
        0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
    ];

    public FileStorageIntegrationTests(DatabaseFixture fixture)
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
        string url = AdminExtractUrl)
    {
        var response = await ScopeClient.SendAsync(
            ScopedPost(url, PdfUpload(), userId, role, companyId));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope =
            await ReadJsonAsync<ExtractionEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        return envelope!.Data!.ExtractionId;
    }

    private static object ConfirmationBody(
        Guid? employeeId,
        Guid? clientEntityId,
        string documentNumber,
        string? fileUrl = null,
        string? fileName = null,
        long? fileSize = null)
        => new
        {
            employeeId,
            clientEntityId,
            type = DocumentType.Passport,
            documentNumber,
            details = new { passportNumber = "P-STORAGE-REQ" },
            issueDate = (DateTime?)null,
            expiryDate = (DateTime?)null,
            fileUrl,
            fileName,
            contentType = "application/pdf",
            fileSize
        };

    private async Task<Guid> ConfirmAndGetDocumentIdAsync(
        string url,
        Guid extractionId,
        object body,
        Guid userId,
        string role,
        string? clientCompanyId = null)
    {
        var response = await ScopeClient.SendAsync(
            ScopedPost(
                url,
                JsonContent.Create(body),
                userId,
                role,
                clientCompanyId));

        await EnsureOk(response, HttpStatusCode.Created);

        var envelope =
            await ReadJsonAsync<ConfirmEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        return envelope!.Data!.DocumentId;
    }

    // ------------------------------------------------------------------
    // Admin: extraction persists the server reference + physical file
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin extraction persists a server-generated FileUrl and the physical file")]
    public async Task Admin_Extract_PersistsServerReferenceAndFile()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId);

            Assert.NotNull(extraction.FileUrl);
            Assert.StartsWith(
                "extractions/",
                extraction.FileUrl,
                StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(extraction.FileUrl));

            var fullPath = Resolve(extraction.FileUrl);

            Assert.True(File.Exists(fullPath));
            Assert.Equal(PdfBytes, File.ReadAllBytes(fullPath));
        });
    }

    // ------------------------------------------------------------------
    // Admin: confirm uses the server reference, not a client fake
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin confirm copies the extraction reference and ignores a fake client FileUrl")]
    public async Task Admin_Confirm_UsesServerReferenceNotClientFileUrl()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.Admin,
            "Admin");

        const string fakeFileUrl =
            "https://storage.example.test/fake.pdf";

        var documentId = await ConfirmAndGetDocumentIdAsync(
            $"/api/v1/admin/documents/extractions/{extractionId}/confirm",
            extractionId,
            ConfirmationBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "DOC-STORAGE-001",
                fileUrl: fakeFileUrl,
                fileName: "fake-name.pdf"),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId);

            var document = db.Documents
                .AsNoTracking()
                .Single(x => x.Id == documentId);

            Assert.NotNull(extraction.FileUrl);
            Assert.Equal(extraction.FileUrl, document.FileUrl);
            Assert.NotEqual(fakeFileUrl, document.FileUrl);

            // Validated upload metadata carries through, not the client body.
            Assert.Equal("passport.pdf", document.FileName);
            Assert.Equal("application/pdf", document.ContentType);
            Assert.Equal(PdfBytes.Length, document.FileSize);
            Assert.Equal("Confirmed", extraction.Status);
        });
    }

    // ------------------------------------------------------------------
    // Agent: CreatedBy + server reference + ownership gate
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent extraction persists CreatedBy + server reference; confirm uses it")]
    public async Task Agent_ExtractAndConfirm_PersistsCreatedAndReference()
    {
        var agentId = TestDataSeeder.Ids.AgentA;
        var extractionId = await ExtractAsync(
            agentId,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString(),
            AgentExtractUrl);

        string? serverReference = null;

        await WithScopeDbContextAsync(db =>
        {
            var extraction = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId);

            Assert.Equal(agentId, extraction.CreatedBy);
            Assert.NotNull(extraction.FileUrl);
            Assert.StartsWith(
                "extractions/",
                extraction.FileUrl,
                StringComparison.Ordinal);

            serverReference = extraction.FileUrl;
            Assert.True(File.Exists(Resolve(extraction.FileUrl)));
        });

        const string fakeFileUrl =
            "https://storage.example.test/fake-agent.pdf";

        var documentId = await ConfirmAndGetDocumentIdAsync(
            $"/api/v1/agent/documents/extractions/{extractionId}/confirm",
            extractionId,
            ConfirmationBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "DOC-STORAGE-AGENT",
                fileUrl: fakeFileUrl),
            agentId,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents
                .AsNoTracking()
                .Single(x => x.Id == documentId);

            Assert.NotNull(serverReference);
            Assert.Equal(serverReference, document.FileUrl);
            Assert.NotEqual(fakeFileUrl, document.FileUrl);
        });
    }

    [Fact(DisplayName =
        "Agent ownership gate still blocks another agent from confirming")]
    public async Task Agent_OtherAgent_CannotConfirm()
    {
        var extractionId = await ExtractAsync(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString(),
            AgentExtractUrl);

        var response = await ScopeClient.SendAsync(
            ScopedPost(
                $"/api/v1/agent/documents/extractions/{extractionId}/confirm",
                JsonContent.Create(
                    ConfirmationBody(
                        TestDataSeeder.Ids.EmployeeA,
                        null,
                        "DOC-X")),
                TestDataSeeder.Ids.AgentB,
                "Agent",
                TestDataSeeder.Ids.CompanyA.ToString()));

        await EnsureOk(response, HttpStatusCode.NotFound);

        await WithScopeDbContextAsync(db =>
        {
            Assert.Empty(
                db.Documents
                    .AsNoTracking()
                    .Where(x => x.DocumentNumber == "DOC-X")
                    .ToList());
        });
    }

    // ------------------------------------------------------------------
    // Legacy compatibility: NULL FileUrl keeps current client behavior
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Legacy extraction with NULL FileUrl keeps the client-supplied location")]
    public async Task LegacyExtraction_NullFileUrl_UsesClientFileUrl()
    {
        var extractionId = Guid.NewGuid();
        const string legacyFileUrl =
            "https://storage.example.test/legacy.pdf";

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(new DocumentExtraction
            {
                Id = extractionId,
                FileName = "legacy.pdf",
                FileUrl = null,
                ContentType = "application/pdf",
                FileSize = 42,
                Status = "Pending",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null,
                ExpiresAt = now.AddHours(24)
            });

            db.SaveChanges();
        });

        var documentId = await ConfirmAndGetDocumentIdAsync(
            $"/api/v1/admin/documents/extractions/{extractionId}/confirm",
            extractionId,
            ConfirmationBody(
                TestDataSeeder.Ids.EmployeeA,
                null,
                "DOC-LEGACY-001",
                fileUrl: legacyFileUrl,
                fileName: "legacy-confirmed.pdf",
                fileSize: 42),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents
                .AsNoTracking()
                .Single(x => x.Id == documentId);

            Assert.Equal(legacyFileUrl, document.FileUrl);
            Assert.Equal("legacy-confirmed.pdf", document.FileName);
            Assert.Equal(42L, document.FileSize);
        });
    }

    // ------------------------------------------------------------------
    // Failure behavior: storage failure does not persist an extraction
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Storage write failure returns 500 and leaves no extraction row")]
    public async Task StorageFailure_LeavesNoExtractionRow()
    {
        var blocker = Path.Combine(
            Path.GetTempPath(),
            $"proms-blocker-{Guid.NewGuid():N}");

        File.WriteAllText(blocker, "block");

        // A directory cannot be created beneath an existing file, so every
        // storage write here throws before any database write.
        var invalidRoot = Path.Combine(blocker, "storage");

        try
        {
            using var factory = new TestApplicationFactory(
                Fixture.Container.GetConnectionString(),
                storageRoot: invalidRoot);

            using var client = factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme =
                                TestIdentityService.SchemeName;
                            options.DefaultChallengeScheme =
                                TestIdentityService.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions,
                            TestIdentityService>(
                            TestIdentityService.SchemeName, _ => { });
                    });
                })
                .CreateClient();

            var response = await client.SendAsync(
                ScopedPost(
                    AdminExtractUrl,
                    PdfUpload(),
                    TestDataSeeder.Ids.Admin,
                    "Admin"));

            Assert.Equal(
                HttpStatusCode.InternalServerError,
                response.StatusCode);

            // The handler stored nothing and never persisted a draft that
            // points at a nonexistent file.
            await WithScopeDbContextAsync(db =>
            {
                Assert.Empty(
                    db.DocumentExtractions
                        .AsNoTracking()
                        .ToList());
            });

            Assert.False(Directory.Exists(invalidRoot));
        }
        finally
        {
            if (File.Exists(blocker))
            {
                File.Delete(blocker);
            }
        }
    }

    // ------------------------------------------------------------------
    // Response envelopes
    // ------------------------------------------------------------------

    private sealed class ExtractionEnvelope
    {
        public bool Success { get; set; }

        public ExtractionPayload? Data { get; set; }
    }

    private sealed class ExtractionPayload
    {
        public Guid ExtractionId { get; set; }
    }

    private sealed class ConfirmEnvelope
    {
        public bool Success { get; set; }

        public ConfirmPayload? Data { get; set; }
    }

    private sealed class ConfirmPayload
    {
        public Guid DocumentId { get; set; }
    }
}