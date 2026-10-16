using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Security;

/// <summary>
/// Verifies the Day 6 H1 ownership fix: <c>DocumentExtraction.CreatedBy</c>
/// ties a Pending extraction draft to the agent who created it, and only that
/// agent may confirm it. Every scenario creates the extraction through the
/// real JWT + multipart extract endpoint, so <c>CreatedBy</c> flows from
/// <c>ICurrentUserService</c> into the persisted row. Test C clears both
/// agents' owner scope so the 404 can only originate from the CreatedBy gate,
/// not from the resource-scope checks.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExtractionOwnershipTests : IntegrationTestBase
{
    private static readonly Guid SharedTaskId =
        Guid.Parse("00000000-0000-0000-0000-000000000888");

    private static readonly byte[] PdfBytes =
    [
        0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, // "%PDF-1.4"
        0x0a, 0x25, 0x25, 0x45, 0x4f, 0x46, 0x0a        // "%%EOF\n"
    ];

    public ExtractionOwnershipTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers (real JWT pipeline + multipart upload)
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

    private async Task<(HttpStatusCode status, Guid? extractionId)>
        ExtractAsync(Guid userId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/agent/documents/extract");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", AgentToken(userId));

        using var file = new ByteArrayContent(
            PdfBytes);
        file.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        using var form = new MultipartFormDataContent();
        form.Add(file, "file", "document.pdf");
        form.Add(new StringContent("Passport"), "documentType");
        request.Content = form;

        var response = await AuthClient.SendAsync(request);

        Guid? extractionId = null;
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var envelope =
                await ReadJsonAsync<ExtractEnvelope>(response);
            extractionId = envelope?.Data?.ExtractionId;
        }

        return (response.StatusCode, extractionId);
    }

    private async Task<HttpStatusCode> ConfirmAsync(
        Guid userId,
        Guid extractionId,
        Guid? employeeId,
        Guid? clientEntityId,
        string documentNumber)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/agent/documents/extractions/" +
            $"{extractionId}/confirm")
        {
            Content = JsonContent.Create(new
            {
                employeeId,
                clientEntityId,
                type = DocumentType.Passport,
                documentNumber,
                details = new { passportNumber = documentNumber }
            })
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", AgentToken(userId));

        var response = await AuthClient.SendAsync(request);
        return response.StatusCode;
    }

    /// <summary>
    /// Gives Agent B a task pointing at Document A (= Employee A), so
    /// Employee A is inside BOTH agents' owner scope. This isolates the
    /// ownership gate from the resource-scope checks.
    /// </summary>
    private Task SeedSharedScopeAsync()
        => WithScopeDbContextAsync(db =>
        {
            db.RenewalTasks.Add(new RenewalTask
            {
                Id = SharedTaskId,
                DocumentId = TestDataSeeder.Ids.DocumentA,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = TestDataSeeder.Ids.AgentB,
                Status = RenewalTaskStatus.Updated,
                ServiceFeeAmount = 150m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A: creator agent confirms its own extraction (201) and CreatedBy is persisted")]
    public async Task A_Creator_ConfirmsOwnExtraction_Returns201()
    {
        var (extractStatus, extractionId) =
            await ExtractAsync(TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.OK, extractStatus);
        Assert.NotNull(extractionId);

        // Prove the extract handler persisted CreatedBy from the JWT.
        await WithScopeDbContextAsync(db =>
        {
            var row = db.DocumentExtractions
                .AsNoTracking()
                .Single(x => x.Id == extractionId!.Value);
            Assert.Equal(TestDataSeeder.Ids.AgentA, row.CreatedBy);
        });

        var confirm = await ConfirmAsync(
            TestDataSeeder.Ids.AgentA,
            extractionId!.Value,
            TestDataSeeder.Ids.EmployeeA,
            null,
            "DOC-A-H1");

        Assert.Equal(HttpStatusCode.Created, confirm);
    }

    [Fact(DisplayName =
        "B: another agent cannot confirm a Pending extraction (404)")]
    public async Task B_OtherAgent_ConfirmsExtraction_Returns404()
    {
        var (extractStatus, extractionId) =
            await ExtractAsync(TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.OK, extractStatus);
        Assert.NotNull(extractionId);

        var confirm = await ConfirmAsync(
            TestDataSeeder.Ids.AgentB,
            extractionId!.Value,
            TestDataSeeder.Ids.EmployeeA,
            null,
            "DOC-A-H1");

        Assert.Equal(HttpStatusCode.NotFound, confirm);
    }

    [Fact(DisplayName =
        "C: owner in BOTH scopes - B still cannot consume A's extraction (404, ownership not scope)")]
    public async Task C_SharedOwner_OtherAgent_Returns404()
    {
        await SeedSharedScopeAsync();

        var (extractStatus, extractionId) =
            await ExtractAsync(TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.OK, extractStatus);
        Assert.NotNull(extractionId);

        // Employee A is in Agent B's scope (via the shared task), so the
        // only gate that can fail is CreatedBy ownership.
        var confirm = await ConfirmAsync(
            TestDataSeeder.Ids.AgentB,
            extractionId!.Value,
            TestDataSeeder.Ids.EmployeeA,
            null,
            "DOC-A-H1");

        Assert.Equal(HttpStatusCode.NotFound, confirm);
    }

    [Fact(DisplayName =
        "C+control: shared owner - the creator (B) confirms successfully (201), proving scope passes")]
    public async Task CPlus_SharedOwner_Creator_Returns201()
    {
        await SeedSharedScopeAsync();

        var (extractStatus, extractionId) =
            await ExtractAsync(TestDataSeeder.Ids.AgentB);

        Assert.Equal(HttpStatusCode.OK, extractStatus);
        Assert.NotNull(extractionId);

        // Agent B has CreatedBy = AgentB and scope over Employee A, so the
        // only differing gate versus test C is ownership. 201 proves scope
        // alone would have let B through in C.
        var confirm = await ConfirmAsync(
            TestDataSeeder.Ids.AgentB,
            extractionId!.Value,
            TestDataSeeder.Ids.EmployeeA,
            null,
            "DOC-B-H1");

        Assert.Equal(HttpStatusCode.Created, confirm);
    }

    [Fact(DisplayName =
        "D: out-of-scope owner keeps the existing 404 behavior")]
    public async Task D_OutOfScopeOwner_Returns404()
    {
        var (extractStatus, extractionId) =
            await ExtractAsync(TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.OK, extractStatus);
        Assert.NotNull(extractionId);

        // Employee B belongs to Agent B's world; Agent A has no scope.
        var confirm = await ConfirmAsync(
            TestDataSeeder.Ids.AgentA,
            extractionId!.Value,
            TestDataSeeder.Ids.EmployeeB,
            null,
            "DOC-A-H1");

        Assert.Equal(HttpStatusCode.NotFound, confirm);
    }

    [Fact(DisplayName =
        "E: confirming an already-processed extraction keeps the 409 behavior")]
    public async Task E_AlreadyProcessed_Returns409()
    {
        var (extractStatus, extractionId) =
            await ExtractAsync(TestDataSeeder.Ids.AgentA);

        Assert.Equal(HttpStatusCode.OK, extractStatus);
        Assert.NotNull(extractionId);

        var first = await ConfirmAsync(
            TestDataSeeder.Ids.AgentA,
            extractionId!.Value,
            TestDataSeeder.Ids.EmployeeA,
            null,
            "DOC-A-H1");
        Assert.Equal(HttpStatusCode.Created, first);

        var second = await ConfirmAsync(
            TestDataSeeder.Ids.AgentA,
            extractionId!.Value,
            TestDataSeeder.Ids.EmployeeA,
            null,
            "DOC-A-H1");
        Assert.Equal(HttpStatusCode.Conflict, second);
    }

    [Fact(DisplayName =
        "F: confirming a nonexistent extraction returns 404")]
    public async Task F_NonexistentExtraction_Returns404()
    {
        var confirm = await ConfirmAsync(
            TestDataSeeder.Ids.AgentA,
            TestDataSeeder.Ids.Unknown,
            TestDataSeeder.Ids.EmployeeA,
            null,
            "DOC-A-H1");

        Assert.Equal(HttpStatusCode.NotFound, confirm);
    }

    // ------------------------------------------------------------------
    // Deserialization envelope for the extract response
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
}