using System.Net;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Storage;

/// <summary>
/// D1: authorized download of a CONFIRMED document's stored file.
///
/// The scan endpoints return a relative storage reference that is not
/// browser-retrievable, so these endpoints stream the actual bytes instead.
/// Verified here: exact bytes, the stored validated content type, a
/// Content-Disposition that cannot be header-injected, and 404 for every
/// failure mode (missing, soft-deleted, no file, unsupported type, unreadable
/// or traversal-rejected reference, out-of-scope caller). The internal storage
/// reference is never returned in a response body.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentFileDownloadTests : IntegrationTestBase
{
    private const string AdminFileUrl =
        "/api/v1/admin/documents/{0}/file";

    private const string AgentFileUrl =
        "/api/v1/agent/documents/{0}/file";

    private const string ClientFileUrl =
        "/api/v1/client/documents/{0}/file";

    private static readonly byte[] PdfBytes =
    [
        0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
        0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
    ];

    public DocumentFileDownloadTests(DatabaseFixture fixture)
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

    /// <summary>
    /// Writes real bytes into the test storage root and returns the
    /// server-shaped reference, exactly as extraction would have stored it.
    /// </summary>
    private string WriteStoredFile(byte[]? bytes = null)
    {
        var reference = $"extractions/{Guid.NewGuid():N}.pdf";

        var fullPath = Resolve(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes ?? PdfBytes);

        return reference;
    }

    /// <summary>
    /// Inserts an isolated Document (unique id + unique number, so the
    /// owner/type/number uniqueness index is never violated) owned by a
    /// CompanyA employee, which makes it visible to the CompanyA client and to
    /// any agent holding a task that references it.
    /// </summary>
    private Task<Guid> SeedDocumentAsync(
        string? fileUrl,
        string? contentType = "application/pdf",
        string? fileName = "passport.pdf",
        bool isDeleted = false)
        => SeedDocumentAsync(
            fileUrl,
            contentType,
            fileName,
            isDeleted,
            linkToAgentA: false,
            TestDataSeeder.Ids.AgentB);

    private async Task<Guid> SeedDocumentAsync(
        string? fileUrl,
        string? contentType,
        string? fileName,
        bool isDeleted,
        bool linkToAgentA,
        Guid? taskAssignedAgentId)
    {
        var documentId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.Passport,
                DocumentNumber = $"D1-DL-{documentId:N}"[..24],
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddYears(4),
                FileUrl = fileUrl,
                FileName = fileName,
                ContentType = contentType,
                FileSize = fileUrl is null ? null : PdfBytes.Length,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now : null
            });

            if (linkToAgentA)
            {
                // Only a task-linked document is inside an agent's portfolio
                // scope, so the link is what makes this document reachable.
                db.RenewalTasks.Add(new RenewalTask
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                    AssignedStaffId = taskAssignedAgentId,
                    Status = RenewalTaskStatus.Submitted,
                    ServiceFeeAmount = 150m,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            db.SaveChanges();
        });

        return documentId;
    }

    private Task SetDocumentPropertyAsync(
        Guid documentId,
        Action<Document> mutate)
        => WithScopeDbContextAsync(db =>
        {
            var row = db.Documents.Single(x => x.Id == documentId);
            mutate(row);
            db.SaveChanges();
        });

    private static async Task AssertNotFoundContractAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var envelope = await ReadJsonAsync<ErrorEnvelope>(response);

        Assert.NotNull(envelope?.Error);
        Assert.Equal("DOCUMENT_FILE_NOT_FOUND", envelope.Error.Code);

        // A 404 is always the JSON error contract, never file bytes.
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEqual(PdfBytes, body);
    }

    // ------------------------------------------------------------------
    // Admin
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin downloads a confirmed document's file (200, exact bytes)")]
    public async Task AdminDownloadsConfirmedDocumentFile()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        Assert.Equal(
            "application/pdf",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            PdfBytes,
            await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName =
        "Admin download response never contains the storage reference")]
    public async Task AdminDownloadDoesNotExposeStorageReference()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var served = await response.Content.ReadAsByteArrayAsync();

        // The response is the raw file: the storage key is neither returned
        // as a header nor embedded in the body.
        Assert.Equal(PdfBytes, served);
        Assert.DoesNotContain(
            "extractions/",
            response.Content.Headers.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName =
        "Missing document returns 404")]
    public async Task AdminMissingDocument_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(
                    AdminFileUrl,
                    TestDataSeeder.Ids.Unknown),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Soft-deleted document returns 404 (its file is unreachable)")]
    public async Task AdminSoftDeletedDocument_Returns404()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        await SetDocumentPropertyAsync(documentId, row =>
        {
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
        });

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Document with no stored file returns 404")]
    public async Task AdminNullFileUrl_Returns404()
    {
        var documentId = await SeedDocumentAsync(null);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Physical file removed from storage returns 404")]
    public async Task AdminMissingPhysicalFile_Returns404()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        File.Delete(Resolve(reference));

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Unsupported stored content type is rejected (404, no bytes served)")]
    public async Task AdminUnsupportedContentType_Returns404()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(
            reference,
            contentType: "text/html");

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Content-Disposition is sanitized (no header injection)")]
    public async Task AdminContentDispositionIsSanitized()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(
            reference,
            fileName: "scan\r\nX-Injected: yes.pdf");

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var hasDisposition =
            response.Content.Headers.TryGetValues(
                "Content-Disposition", out var values);

        if (hasDisposition)
        {
            foreach (var value in values!)
            {
                Assert.DoesNotContain(
                    "\r",
                    value,
                    StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "\n",
                    value,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact(DisplayName =
        "Tampered traversal storage reference cannot escape the storage root")]
    public async Task TraversalReferenceCannotEscapeStorageRoot()
    {
        var decoyPath = Path.Combine(
            Path.GetDirectoryName(FactoryStorageRoot)!,
            "d1-decoy.pdf");

        if (File.Exists(decoyPath))
        {
            File.Delete(decoyPath);
        }

        try
        {
            byte[] decoy = [0x44, 0x31, 0x44, 0x45, 0x43, 0x4f, 0x59];
            File.WriteAllBytes(decoyPath, decoy);

            var documentId = await SeedDocumentAsync(
                "../../d1-decoy.pdf");

            var response = await ScopeClient.SendAsync(
                ScopeGet(
                    string.Format(AdminFileUrl, documentId),
                    TestDataSeeder.Ids.Admin,
                    "Admin"));

            await AssertNotFoundContractAsync(response);

            // The decoy outside the root is untouched.
            Assert.Equal(decoy, File.ReadAllBytes(decoyPath));
        }
        finally
        {
            if (File.Exists(decoyPath))
            {
                File.Delete(decoyPath);
            }
        }
    }

    [Fact(DisplayName =
        "Absolute storage reference is rejected (404)")]
    public async Task AbsoluteStorageReferenceIsRejected()
    {
        var documentId = await SeedDocumentAsync(
            Path.Combine(Path.GetTempPath(), "d1-absolute.pdf"));

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AdminFileUrl, documentId),
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await AssertNotFoundContractAsync(response);
    }

    // ------------------------------------------------------------------
    // Agent (task-linked portfolio scope)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent downloads a task-linked document's file (200)")]
    public async Task AgentDownloadsInScopeDocumentFile()
    {
        var reference = WriteStoredFile();

        var documentId = await SeedDocumentAsync(
            reference,
            contentType: "application/pdf",
            fileName: "passport.pdf",
            isDeleted: false,
            linkToAgentA: true,
            taskAssignedAgentId: TestDataSeeder.Ids.AgentA);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AgentFileUrl, documentId),
                TestDataSeeder.Ids.AgentA,
                "Agent",
                TestDataSeeder.Ids.CompanyA.ToString()));

        await EnsureOk(response, HttpStatusCode.OK);

        Assert.Equal(
            PdfBytes,
            await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName =
        "Agent cannot download a document outside their portfolio (404)")]
    public async Task AgentCannotDownloadOutOfScopeDocument()
    {
        var reference = WriteStoredFile();

        // The document is task-linked, but to AgentB.
        var documentId = await SeedDocumentAsync(
            reference,
            contentType: "application/pdf",
            fileName: "passport.pdf",
            isDeleted: false,
            linkToAgentA: true,
            taskAssignedAgentId: TestDataSeeder.Ids.AgentB);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AgentFileUrl, documentId),
                TestDataSeeder.Ids.AgentA,
                "Agent",
                TestDataSeeder.Ids.CompanyA.ToString()));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Agent cannot download an untasked document (404)")]
    public async Task AgentCannotDownloadUntaskedDocument()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(AgentFileUrl, documentId),
                TestDataSeeder.Ids.AgentA,
                "Agent",
                TestDataSeeder.Ids.CompanyA.ToString()));

        await AssertNotFoundContractAsync(response);
    }

    // ------------------------------------------------------------------
    // Client (own-company scope, read-only)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Client downloads its own company's document file (200)")]
    public async Task ClientDownloadsOwnCompanyDocumentFile()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(ClientFileUrl, documentId),
                TestDataSeeder.Ids.ClientUserA,
                "Client",
                TestDataSeeder.Ids.CompanyA.ToString()));

        await EnsureOk(response, HttpStatusCode.OK);

        Assert.Equal(
            "application/pdf",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            PdfBytes,
            await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName =
        "Client cannot download another company's document file (404)")]
    public async Task ClientCannotDownloadOtherCompanyDocumentFile()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(ClientFileUrl, documentId),
                TestDataSeeder.Ids.ClientUserB,
                "Client",
                TestDataSeeder.Ids.CompanyB.ToString()));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Client cannot reach the document file for a soft-deleted document (404)")]
    public async Task ClientSoftDeletedDocument_Returns404()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(
            reference,
            contentType: "application/pdf",
            fileName: "passport.pdf",
            isDeleted: true,
            linkToAgentA: false,
            taskAssignedAgentId: TestDataSeeder.Ids.AgentB);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                string.Format(ClientFileUrl, documentId),
                TestDataSeeder.Ids.ClientUserA,
                "Client",
                TestDataSeeder.Ids.CompanyA.ToString()));

        await AssertNotFoundContractAsync(response);
    }

    [Fact(DisplayName =
        "Unauthenticated caller cannot reach a document file (401)")]
    public async Task UnauthenticatedCannotDownloadDocumentFile()
    {
        var reference = WriteStoredFile();
        var documentId = await SeedDocumentAsync(reference);

        var response = await AuthClient.GetAsync(
            string.Format(AdminFileUrl, documentId));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Response envelopes
    // ------------------------------------------------------------------

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
