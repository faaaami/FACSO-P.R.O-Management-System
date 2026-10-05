using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// D3: locks in the exact behaviour of the 90-day document expiry window.
///
/// The predicate in <see cref="DocumentExpiryWindowCondition"/> is
/// <c>expiry_date &gt;= CURRENT_TIMESTAMP AND expiry_date &lt;=
/// CURRENT_TIMESTAMP + (@Days * INTERVAL '1 day')</c> — both edges
/// inclusive. The job itself always calls this with
/// <c>includeExpired: false</c>.
///
/// Before D3 the whole suite seeded documents 30 days out, so a regression
/// that widened the window (or dropped the "not already expired" guard) would
/// still have passed every test. These tests pin both edges from the inside
/// and the outside.
///
/// There is no clock abstraction in this codebase, so the window is probed
/// with <see cref="DateTime.UtcNow"/> offsets. Exact +90 days is deliberately
/// never seeded: both edges are inclusive and CURRENT_TIMESTAMP advances
/// while the test runs, which would make the boundary assertion flaky.
/// One day of margin on each side is used instead.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentExpiryWindowTests : IntegrationTestBase
{
    private const int WindowDays = 90;
    private const int PageSize = 100;

    private static readonly Guid EmployeeA = TestDataSeeder.Ids.EmployeeA;

    // Matches DatabaseFixture: ["RenewalAutomation:UserId"].
    private static readonly Guid AutomationUserId =
        Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    public DocumentExpiryWindowTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private Task<ExpiringDocumentListItemDto> ExpiringAsync(
        Guid id)
        => ExpiringAsync(id, WindowDays, 1, PageSize);

    private async Task<ExpiringDocumentListItemDto> ExpiringAsync(
        Guid id,
        int days,
        int page,
        int pageSize)
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IDocumentExpiryReadRepository>();

        var result = await repository.GetExpiringAsync(
            days,
            page,
            pageSize,
            includeExpired: false,
            CancellationToken.None);

        return result.Items.FirstOrDefault(x => x.Id == id)!;
    }

    private async Task<List<Guid>> ExpiringIdsAsync(
        int days,
        int page,
        int pageSize)
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IDocumentExpiryReadRepository>();

        var result = await repository.GetExpiringAsync(
            days,
            page,
            pageSize,
            includeExpired: false,
            CancellationToken.None);

        return result.Items.Select(x => x.Id).ToList();
    }

    private async Task<int> TotalCountAsync(
        int days,
        int page,
        int pageSize)
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IDocumentExpiryReadRepository>();

        var result = await repository.GetExpiringAsync(
            days,
            page,
            pageSize,
            includeExpired: false,
            CancellationToken.None);

        return result.TotalCount;
    }

    /// <summary>
    /// The shared seed (including MockClientCompanySeeder) already plants
    /// documents inside the 90-day window, so absolute counts are never
    /// trustworthy. Every assertion here is scoped to a document id this
    /// test created.
    /// </summary>
    private Task SeedDocumentAsync(
        Guid id,
        DateTime? expiryDate,
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
                DocumentNumber = $"DOC-WIN-{id.ToString("N")[..8]}",
                IssueDate = now.AddYears(-1),
                ExpiryDate = expiryDate,
                FileUrl = $"https://storage.test/{id:N}",
                FileName = "window.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now : null
            });

            db.SaveChanges();
        });

    private static DateTime InDays(int days)
        => DateTime.UtcNow.AddDays(days);

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

    private async Task RunExpiryJobAsync()
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var job = scope.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();

        await job.ExecuteAsync();
    }

    private async Task<int> ActiveTaskCountAsync(Guid documentId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
            count = db.RenewalTasks.Count(x =>
                x.DocumentId == documentId &&
                !x.IsDeleted &&
                x.Status != RenewalTaskStatus.Updated));

        return count;
    }

    // ==================================================================
    // A / B - inside the window
    // ==================================================================

    [Fact(DisplayName =
        "D3-WIN1: a document expiring in 89 days is inside the 90-day window")]
    public async Task Window_Includes_DocumentExpiringIn89Days()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(89));

        Assert.NotNull(await ExpiringAsync(id));
    }

    [Fact(DisplayName =
        "D3-WIN2: a document expiring in 1 day is inside the 90-day window")]
    public async Task Window_Includes_DocumentExpiringIn1Day()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(1));

        Assert.NotNull(await ExpiringAsync(id));
    }

    // ==================================================================
    // C / D - outside the window
    // ==================================================================

    [Fact(DisplayName =
        "D3-WIN3: an already expired document is excluded (includeExpired: false)")]
    public async Task Window_Excludes_AlreadyExpiredDocument()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-1));

        Assert.Null(await ExpiringAsync(id));
    }

    [Fact(DisplayName =
        "D3-WIN4: a document expiring in 91 days is outside the 90-day window")]
    public async Task Window_Excludes_DocumentExpiringIn91Days()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(91));

        Assert.Null(await ExpiringAsync(id));
    }

    // ==================================================================
    // E / F - structural exclusions
    // ==================================================================

    [Fact(DisplayName =
        "D3-WIN5: a document with a null expiry date is excluded")]
    public async Task Window_Excludes_NullExpiryDate()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, expiryDate: null);

        Assert.Null(await ExpiringAsync(id));
    }

    [Fact(DisplayName =
        "D3-WIN6: a soft-deleted document is excluded even when it expires inside the window")]
    public async Task Window_Excludes_SoftDeletedDocument()
    {
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(30), isDeleted: true);

        Assert.Null(await ExpiringAsync(id));
    }

    // ==================================================================
    // G - pagination
    // ==================================================================

    [Fact(DisplayName =
        "D3-WIN7: 150 qualifying documents paginate 100/50 with no skipped or duplicated ids")]
    public async Task Window_Pagination_150Documents_NoSkipsOrDuplicates()
    {
        const int total = 150;

        // Soft-delete everything the shared seed planted so the only live
        // documents are the 150 created here and the totals are exact.
        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            foreach (var document in db.Documents
                .IgnoreQueryFilters()
                .ToList())
            {
                document.IsDeleted = true;
                document.DeletedAt = now;
            }

            db.SaveChanges();
        });

        // Distinct, increasing expiry dates so "ORDER BY expiry_date ASC"
        // is a total order. Equal keys would leave the page split at the
        // database's discretion and the assertion flaky.
        var seeded = new List<Guid>();
        var now2 = DateTime.UtcNow;

        await WithScopeDbContextAsync(db =>
        {
            for (var i = 0; i < total; i++)
            {
                var id = Guid.NewGuid();
                seeded.Add(id);

                db.Documents.Add(new Document
                {
                    Id = id,
                    EmployeeId = EmployeeA,
                    ClientEntityId = null,
                    Type = DocumentType.Passport,
                    DocumentNumber = $"DOC-PAGE-{i:D4}",
                    IssueDate = now2.AddYears(-1),
                    ExpiryDate = now2.AddDays(89).AddMinutes(i),
                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = "page.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now2,
                    UpdatedAt = now2,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            db.SaveChanges();
        });

        var page1 = await ExpiringIdsAsync(WindowDays, 1, PageSize);
        var page2 = await ExpiringIdsAsync(WindowDays, 2, PageSize);
        var page3 = await ExpiringIdsAsync(WindowDays, 3, PageSize);

        Assert.Equal(100, page1.Count);
        Assert.Equal(50, page2.Count);
        Assert.Empty(page3);

        Assert.Equal(
            total,
            await TotalCountAsync(WindowDays, 1, PageSize));

        // Ordered by expiry ascending, so page 1 is the earliest 100.
        Assert.Equal(
            seeded.Take(100).ToList(),
            page1);
        Assert.Equal(
            seeded.Skip(100).ToList(),
            page2);

        var combined = page1.Concat(page2).ToList();
        Assert.Equal(total, combined.Count);
        Assert.Equal(total, combined.Distinct().Count());
    }

    // ==================================================================
    // Job level - the window must drive task creation
    // ==================================================================

    [Fact(DisplayName =
        "D3-WIN8: the expiry job creates a task for a +89d document and none for +91d or expired documents")]
    public async Task Job_CreatesTask_OnlyForDocumentsInsideWindow()
    {
        await SeedAutomationUserAsync();

        var inside = Guid.NewGuid();
        var beyond = Guid.NewGuid();
        var expired = Guid.NewGuid();

        await SeedDocumentAsync(inside, InDays(89));
        await SeedDocumentAsync(beyond, InDays(91));
        await SeedDocumentAsync(expired, InDays(-1));

        await RunExpiryJobAsync();

        // The job swallows per-document exceptions, so this asserts the
        // database rows rather than the job completing successfully.
        Assert.Equal(1, await ActiveTaskCountAsync(inside));
        Assert.Equal(0, await ActiveTaskCountAsync(beyond));
        Assert.Equal(0, await ActiveTaskCountAsync(expired));
    }
}
