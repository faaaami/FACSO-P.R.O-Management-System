using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Application.Common.Services;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// P10.4 / R-13: locks in that the shared proactive expiry query only offers
/// <b>active</b> documents to callers.
///
/// <see cref="IDocumentExpiryReadRepository.GetExpiringAsync"/> has two
/// callers, and the single <c>is_active = true</c> predicate governs both:
///
///   1. <c>DocumentExpiryJob</c> (Hangfire id <c>document-expiry-scanner</c>,
///      daily). Before this predicate the query handed inactive documents to
///      <c>RenewalTaskCreationService.CreateAsync</c>, which refuses them by
///      design with "Cannot create a renewal task for an inactive document."
///      The job caught that per document, logged an error and counted it as a
///      failure, so every run emitted one false failure per inactive
///      expiring document.
///   2. <c>GET /api/v1/admin/documents/expiring</c>, which therefore also
///      listed inactive documents while the dashboard expiry alerts, the
///      employee expiry query and the overdue sweep all excluded them.
///
/// So the data outcome was already safe (no bad task was ever created) while
/// the reported outcome was not. These tests pin both callers to the active
/// definition, and pin the two behaviours that the new predicate must not
/// disturb: the <c>is_deleted</c> exclusion and the
/// <c>IsDeleted = false AND Status != 6</c> renewal-after-terminal index.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentExpiryInactiveTests : IntegrationTestBase
{
    private const int WindowDays = 90;

    // GetExpiringDocumentsQueryValidator caps PageSize at 100, and the
    // scanner pages at 100 too.
    private const int PageSize = 100;

    private static readonly Guid EmployeeA = TestDataSeeder.Ids.EmployeeA;

    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;

    /// <summary>Matches DatabaseFixture's RenewalAutomation:UserId.</summary>
    private static readonly Guid AutomationUserId =
        Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    public DocumentExpiryInactiveTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static DateTime InDays(int days)
        => DateTime.UtcNow.AddDays(days);

    private static string DocumentNumberFor(Guid id)
        => $"DOC-INACT-{id.ToString("N")[..8]}";

    /// <summary>
    /// Plants a document owned by an employee of company A, so
    /// RenewalTaskCreationService can resolve a company for it. Owner choice
    /// and expiry are deliberate: this suite varies only isActive/isDeleted.
    /// </summary>
    private Task SeedDocumentAsync(
        Guid id,
        DateTime? expiryDate,
        bool isActive = true,
        bool isDeleted = false)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = id,
                EmployeeId = EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber = DocumentNumberFor(id),
                IssueDate = now.AddYears(-1),
                ExpiryDate = expiryDate,
                FileUrl = $"https://storage.test/{id:N}",
                FileName = "expiry-inactive.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now : null
            });

            db.SaveChanges();
        });

    /// <summary>
    /// DatabaseFixture only configures <c>RenewalAutomation:UserId</c>; it does
    /// not seed that user. RenewalTaskCreationService rejects an unknown
    /// actor, so without this every document in the window would fail in the
    /// job and the T2 log assertions would pass for the wrong reason.
    /// </summary>
    private Task SeedAutomationUserAsync()
        => WithScopeDbContextAsync(db =>
        {
            if (db.Users.Any(u => u.Id == AutomationUserId))
            {
                return;
            }

            var now = DateTime.UtcNow;

            db.Users.Add(new User
            {
                Id = AutomationUserId,
                FullName = "Renewal Automation",
                Email = "automation@test.local",
                PasswordHash = "not-used",
                Role = UserRole.Admin,
                IsActive = true,
                IsApproved = true,
                EmailVerified = true,
                ClientCompanyId = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

    /// <summary>
    /// Seeds an existing task in a terminal (Updated) state, to prove the
    /// documented renewal-after-terminal behaviour still holds.
    /// </summary>
    private Task SeedUpdatedTerminalTaskAsync(Guid documentId)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ClientCompanyId = CompanyA,
                AssignedStaffId = null,
                Status = RenewalTaskStatus.Updated,
                BlockedReason = null,
                BlockedSince = null,
                CompletedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

    private async Task<List<Guid>> ExpiringIdsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IDocumentExpiryReadRepository>();

        var result = await repository.GetExpiringAsync(
            WindowDays,
            1,
            PageSize,
            includeExpired: false,
            CancellationToken.None);

        return result.Items.Select(x => x.Id).ToList();
    }

    /// <summary>
    /// Runs the real <see cref="DocumentExpiryJob"/> over a real database,
    /// with the job's own <c>ILogger</c> captured.
    ///
    /// The collaborator is the logger only. The repository and the renewal
    /// task service are the real ones, because the behaviour under test is
    /// which rows the query offers, not what the service would do with an
    /// inactive one.
    /// </summary>
    private async Task<CapturingLogger<DocumentExpiryJob>> RunExpiryJobAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();

        var logger = new CapturingLogger<DocumentExpiryJob>();

        var job = new DocumentExpiryJob(
            scope.ServiceProvider
                .GetRequiredService<IDocumentExpiryReadRepository>(),
            new RenewalTaskCreationService(
                scope.ServiceProvider
                    .GetRequiredService<IApplicationDbContext>()),
            logger,
            AutomationUserId);

        await job.ExecuteAsync();

        return logger;
    }

    private async Task<List<RenewalTaskStatus>> TaskStatusesAsync(
        Guid documentId)
    {
        var statuses = new List<RenewalTaskStatus>();

        await WithScopeDbContextAsync(db =>
            statuses = db.RenewalTasks
                .Where(x => x.DocumentId == documentId)
                .Select(x => x.Status)
                .ToList());

        return statuses;
    }

    // ==================================================================
    // T1 - the discriminator
    // ==================================================================

    [Fact(DisplayName =
        "P10.4-T1: an inactive, non-deleted, in-window document is absent from GetExpiringAsync")]
    public async Task GetExpiringAsync_Excludes_InactiveDocument()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(30), isActive: false);

        Assert.DoesNotContain(id, await ExpiringIdsAsync());
    }

    // ==================================================================
    // T2 - scanner must not reach the service
    // ==================================================================

    [Fact(DisplayName =
        "P10.4-T2: the proactive scanner neither creates a task for an inactive document nor logs a failure for it")]
    public async Task ExpiryJob_Never_Offers_InactiveDocument_ToCreationService()
    {
        var id = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(id, InDays(30), isActive: false);

        var logger = await RunExpiryJobAsync();

        // No task of any status exists for the inactive document.
        Assert.Empty(await TaskStatusesAsync(id));

        // The job logs "Failed to process expiring document {Id}." and then
        // the InvalidOperationException text. Asserting on both is what proves
        // the document never reached the creation service: merely asserting
        // "no task" would already hold before the fix, because the service
        // throws before inserting.
        Assert.DoesNotContain(
            logger.Entries,
            e => e.Contains(
                $"Failed to process expiring document {id}"));

        Assert.DoesNotContain(
            logger.Entries,
            e => e.Contains("inactive document"));
    }

    // ==================================================================
    // T3 - guard against over-filtering
    // ==================================================================

    [Fact(DisplayName =
        "P10.4-T3: an active, non-deleted, in-window document is still returned and still gets a task")]
    public async Task ActiveDocument_Remains_Eligible()
    {
        var id = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(id, InDays(30));

        Assert.Contains(id, await ExpiringIdsAsync());

        await RunExpiryJobAsync();

        Assert.Equal(
            new[] { RenewalTaskStatus.Submitted },
            await TaskStatusesAsync(id));
    }

    // ==================================================================
    // T4 - the soft-delete predicate must survive
    // ==================================================================

    [Fact(DisplayName =
        "P10.4-T4: a soft-deleted document is still excluded by is_deleted, independent of the new predicate")]
    public async Task SoftDeletedDocument_Remains_Excluded()
    {
        // isActive stays true on purpose: this must be excluded by
        // is_deleted = false, not by the new is_active predicate, otherwise
        // the test would pass even if the soft-delete filter were removed.
        var id = Guid.NewGuid();
        await SeedDocumentAsync(
            id, InDays(30), isActive: true, isDeleted: true);

        Assert.DoesNotContain(id, await ExpiringIdsAsync());
    }

    // ==================================================================
    // T5 - renewal after a terminal task must be unchanged
    // ==================================================================

    [Fact(DisplayName =
        "P10.4-T5: a document whose only task is Updated(terminal) still receives a fresh task")]
    public async Task UpdatedTerminalDocument_Still_Gets_NewTask()
    {
        // Behaviour is preserved exactly as implemented: the renewal path is
        // "IsDeleted = false AND Status != 6", so an existing terminal task
        // does not suppress a new one. Not asserted as new behaviour.
        var id = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(id, InDays(30));
        await SeedUpdatedTerminalTaskAsync(id);

        Assert.Single(await TaskStatusesAsync(id));

        await RunExpiryJobAsync();

        // Order() sorts by enum value, so Submitted(1) precedes Updated(6).
        Assert.Equal(
            new[]
            {
                RenewalTaskStatus.Submitted,
                RenewalTaskStatus.Updated
            },
            (await TaskStatusesAsync(id)).Order().ToList());
    }

    // ==================================================================
    // T6 - the accepted Admin list consequence
    // ==================================================================

    [Fact(DisplayName =
        "P10.4-T6: the Admin expiring list omits inactive documents")]
    public async Task AdminExpiringList_Omits_InactiveDocument()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(30), isActive: false);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                "/api/v1/admin/documents/expiring" +
                $"?days={WindowDays}&page=1&pageSize={PageSize}",
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await EnsureOk(response);

        var body = await ReadJsonAsync<
            ApiResponse<GetExpiringDocumentsResponseDto>>(response);

        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.DoesNotContain(
            id,
            body.Data!.Items.Select(x => x.Id));
    }
}
