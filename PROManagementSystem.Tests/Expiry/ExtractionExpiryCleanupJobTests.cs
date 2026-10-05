using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Storage;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// Proves the background cleanup job (Task 4B.2): only Pending extractions
/// with ExpiresAt &lt;= now are transitioned to Expired with a single
/// DocumentExtractionExpired audit under the automation actor, and only then
/// is the stored file deleted - best-effort and idempotent. Confirmed,
/// already-Expired, NULL-ExpiresAt and soft-deleted rows are never touched.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExtractionExpiryCleanupJobTests : IntegrationTestBase
{
    // Matches TestApplicationFactory: ["RenewalAutomation:UserId"].
    private static readonly Guid AutomationUserId =
        Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    public ExtractionExpiryCleanupJobTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private string Resolve(string reference)
    {
        using var scope = Factory.Services.CreateScope();
        var root = scope.ServiceProvider
            .GetRequiredService<IOptions<StorageOptions>>().Value.RootPath;
        return Path.Combine(
            root,
            reference.Replace(
                '/',
                Path.DirectorySeparatorChar));
    }

    private static string NewReference()
        => $"extractions/{Guid.NewGuid():N}.pdf";

    private void CreatePhysicalFile(string reference)
    {
        var path = Resolve(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "dummy");
    }

    private async Task SeedAutomationUserAsync()
        => await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Users.Add(new User
            {
                Id = AutomationUserId,
                FullName = "Renewal Automation",
                Email = "automation@internal.local",
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

    private async Task SeedExtractionAsync(
        Guid id,
        string status,
        DateTime? expiresAt,
        string? fileUrl = null,
        bool isDeleted = false)
        => await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(
                new DocumentExtraction
                {
                    Id = id,
                    FileName = "upload.pdf",
                    FileUrl = fileUrl,
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    Status = status,
                    ExpiresAt = expiresAt,
                    CreatedBy = null,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = isDeleted,
                    DeletedAt = isDeleted ? now : null
                });
            db.SaveChanges();
        });

    private async Task RunJobAsync(
        CancellationToken cancellationToken = default)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider
            .GetRequiredService<ExtractionExpiryCleanupJob>();
        await job.ExecuteAsync(cancellationToken);
    }

    private async Task AssertStatusAsync(Guid id, string expected)
        => await WithScopeDbContextAsync(db =>
        {
            Assert.Equal(
                expected,
                db.DocumentExtractions.First(x => x.Id == id).Status);
        });

    private async Task AssertSoftDeletedStatusAsync(
        Guid id,
        string expected)
        => await WithScopeDbContextAsync(db =>
        {
            Assert.Equal(
                expected,
                db.DocumentExtractions.IgnoreQueryFilters()
                    .First(x => x.Id == id).Status);
        });

    private async Task<int> CountExpiryAuditsAsync(Guid id)
    {
        var count = 0;
        await WithScopeDbContextAsync(db =>
        {
            count = db.AuditLogs.Count(x =>
                x.Action == "DocumentExtractionExpired" &&
                x.EntityType == "DocumentExtraction" &&
                x.EntityId == id);
        });
        return count;
    }

    private async Task<Guid?> ExpiryAuditUserIdAsync(Guid id)
    {
        Guid? userId = null;
        await WithScopeDbContextAsync(db =>
        {
            userId = db.AuditLogs
                .Where(x =>
                    x.Action == "DocumentExtractionExpired" &&
                    x.EntityType == "DocumentExtraction" &&
                    x.EntityId == id)
                .Select(x => x.UserId)
                .SingleOrDefault();
        });
        return userId;
    }

    // ------------------------------------------------------------------
    // Expired Pending rows: state + audit + file cleanup
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "An expired Pending extraction is marked Expired, its file is " +
        "deleted, and one expiry audit under the automation actor is written")]
    public async Task ExpiredPending_MarkedExpired_FileDeleted_AuditWritten()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        var reference = NewReference();
        CreatePhysicalFile(reference);
        await SeedExtractionAsync(
            id,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            reference);

        await RunJobAsync();

        await AssertStatusAsync(id, "Expired");
        Assert.False(File.Exists(Resolve(reference)));
        Assert.Equal(1, await CountExpiryAuditsAsync(id));
        Assert.Equal(AutomationUserId, await ExpiryAuditUserIdAsync(id));
    }

    [Fact(DisplayName =
        "A non-expired Pending extraction is left untouched and its file remains")]
    public async Task NonExpiredPending_IsLeftUntouched()
    {
        var id = Guid.NewGuid();
        var reference = NewReference();
        CreatePhysicalFile(reference);
        await SeedExtractionAsync(
            id,
            "Pending",
            DateTime.UtcNow.AddHours(24),
            reference);

        await RunJobAsync();

        await AssertStatusAsync(id, "Pending");
        Assert.True(File.Exists(Resolve(reference)));
        Assert.Equal(0, await CountExpiryAuditsAsync(id));
    }

    [Fact(DisplayName =
        "A Confirmed extraction is never touched and its file remains")]
    public async Task Confirmed_IsNeverTouched()
    {
        var id = Guid.NewGuid();
        var reference = NewReference();
        CreatePhysicalFile(reference);
        await SeedExtractionAsync(
            id,
            "Confirmed",
            DateTime.UtcNow.AddDays(-1),
            reference);

        await RunJobAsync();

        await AssertStatusAsync(id, "Confirmed");
        Assert.True(File.Exists(Resolve(reference)));
        Assert.Equal(0, await CountExpiryAuditsAsync(id));
    }

    [Fact(DisplayName =
        "An expired Pending extraction with no stored file is marked " +
        "Expired and the run succeeds")]
    public async Task ExpiredPending_NullFileUrl_IsMarkedExpired()
    {
        var id = Guid.NewGuid();
        await SeedExtractionAsync(
            id,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            fileUrl: null);

        await RunJobAsync();

        await AssertStatusAsync(id, "Expired");
        Assert.Equal(1, await CountExpiryAuditsAsync(id));
    }

    [Fact(DisplayName =
        "A missing physical file is an idempotent success")]
    public async Task MissingPhysicalFile_IsSuccessful()
    {
        var id = Guid.NewGuid();
        var reference = NewReference();
        await SeedExtractionAsync(
            id,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            reference);

        await RunJobAsync();

        await AssertStatusAsync(id, "Expired");
        Assert.Equal(1, await CountExpiryAuditsAsync(id));
        Assert.False(File.Exists(Resolve(reference)));
    }

    // ------------------------------------------------------------------
    // Partial-failure handling
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A failed file deletion does not roll back the Expired state and " +
        "does not stop other rows being processed")]
    public async Task DeleteFailure_KeepsExpiredState_Continues()
    {
        var failedId = Guid.NewGuid();
        var okId = Guid.NewGuid();
        var okReference = NewReference();
        CreatePhysicalFile(okReference);

        // A traversal reference makes DeleteAsync throw, simulating a file
        // deletion failure for one row.
        await SeedExtractionAsync(
            failedId,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            "../escape");
        await SeedExtractionAsync(
            okId,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            okReference);

        await RunJobAsync();

        await AssertStatusAsync(failedId, "Expired");
        await AssertStatusAsync(okId, "Expired");
        Assert.False(File.Exists(Resolve(okReference)));
        Assert.Equal(1, await CountExpiryAuditsAsync(failedId));
        Assert.Equal(1, await CountExpiryAuditsAsync(okId));
    }

    // ------------------------------------------------------------------
    // Multi-row / exclusion / idempotency / cancellation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "All expired Pending extractions in a page are processed")]
    public async Task MultipleExpiredPending_AllProcessed()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var references = new[] { NewReference(), NewReference(), NewReference() };

        for (var i = 0; i < ids.Length; i++)
        {
            CreatePhysicalFile(references[i]);
            await SeedExtractionAsync(
                ids[i],
                "Pending",
                DateTime.UtcNow.AddDays(-1),
                references[i]);
        }

        await RunJobAsync();

        foreach (var id in ids)
        {
            await AssertStatusAsync(id, "Expired");
            Assert.Equal(1, await CountExpiryAuditsAsync(id));
        }

        foreach (var reference in references)
        {
            Assert.False(File.Exists(Resolve(reference)));
        }
    }

    [Fact(DisplayName =
        "Soft-deleted expired extractions are excluded by the query filter")]
    public async Task SoftDeletedExpired_IsExcluded()
    {
        var id = Guid.NewGuid();
        await SeedExtractionAsync(
            id,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            fileUrl: null,
            isDeleted: true);

        await RunJobAsync();

        await AssertSoftDeletedStatusAsync(id, "Pending");
        Assert.Equal(0, await CountExpiryAuditsAsync(id));
    }

    [Fact(DisplayName =
        "Re-running the job never reprocesses an already-Expired row and " +
        "writes no duplicate expiry audits")]
    public async Task RepeatExecution_NoReprocessing_NoDuplicateAudits()
    {
        var id = Guid.NewGuid();
        await SeedExtractionAsync(
            id,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            fileUrl: null);

        await RunJobAsync();
        await RunJobAsync();

        await AssertStatusAsync(id, "Expired");
        Assert.Equal(1, await CountExpiryAuditsAsync(id));
    }

    [Fact(DisplayName =
        "A pre-cancelled token aborts the job without any transitions")]
    public async Task Cancellation_AbortsBeforeAnyTransition()
    {
        var id = Guid.NewGuid();
        await SeedExtractionAsync(
            id,
            "Pending",
            DateTime.UtcNow.AddDays(-1),
            fileUrl: null);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => RunJobAsync(cts.Token));

        await AssertStatusAsync(id, "Pending");
        Assert.Equal(0, await CountExpiryAuditsAsync(id));
    }
}