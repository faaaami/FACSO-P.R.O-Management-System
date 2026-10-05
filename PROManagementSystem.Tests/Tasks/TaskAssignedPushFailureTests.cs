using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Tasks;

/// <summary>
/// P10.7 D-2: a TaskAssigned realtime push failure must not fail an assignment
/// that has already been committed.
///
/// Before the fix AssignRenewalTaskCommandHandler awaited the TaskAssigned send
/// bare and the class had no logger at all. The assignment, its history row and
/// the agent notification were already durable at that point, so a
/// SignalR/Redis transport failure propagated out of the command and returned
/// an HTTP 500 for a committed assignment.
///
/// T3 is the discriminator. T4 pins that the guarded send does not itself
/// produce duplicate records.
///
/// Assignment idempotency on a genuine client retry is explicitly out of
/// P10.7 scope and is NOT tested here.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TaskAssignedPushFailureTests
    : IntegrationTestBase
{
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;

    public TaskAssignedPushFailureTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Seeding: a fresh Submitted task, assigned for the first time
    // ------------------------------------------------------------------

    private async Task<Guid> SeedDocumentAsync()
    {
        var documentId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber =
                    $"DOC-ASSIGNFAIL-{documentId.ToString("N")[..8]}",
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(10),
                FileUrl = $"https://storage.test/{documentId}",
                FileName = "passport.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return documentId;
    }

    private async Task<Guid> SeedUnassignedTaskAsync(Guid documentId)
    {
        var taskId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = taskId,
                DocumentId = documentId,
                ClientCompanyId = CompanyA,
                AssignedStaffId = null,
                Status = RenewalTaskStatus.Submitted,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return taskId;
    }

    private HttpRequestMessage AssignAsAdminRequest(Guid taskId, Guid staffId)
        => ScopePatch(
            $"/api/v1/admin/tasks/{taskId}/assign",
            new { staffId },
            Admin,
            "Admin");

    private async Task<Guid?> ReadAssignedStaffIdAsync(Guid taskId)
    {
        Guid? staffId = null;

        await WithScopeDbContextAsync(db =>
        {
            staffId = db.RenewalTasks
                .Where(x => x.Id == taskId)
                .Select(x => (Guid?)x.AssignedStaffId)
                .FirstOrDefault();
        });

        return staffId;
    }

    private async Task<int> CountHistoryAsync(Guid taskId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.RenewalTaskHistories.Count(x =>
                x.RenewalTaskId == taskId);
        });

        return count;
    }

    private async Task<int> CountAssignmentNotificationsAsync(Guid taskId)
    {
        // The notification body embeds the document number and the agent's full
        // name. Matching on the stable task marker keeps this assertion about
        // the count, not about seeded display names.
        var marker =
            $"TaskAssigned | TaskId:{taskId} |";

        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.Notifications.Count(x =>
                x.Message.StartsWith(marker) &&
                x.UserId == AgentA);
        });

        return count;
    }

    private WebApplicationFactory<Program> BuildFactory(
        out FaultingAssignedPushRealtimeService realtime)
    {
        var capture = new FaultingAssignedPushRealtimeService();

        realtime = capture;

        var factory = Factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(
                services =>
                {
                    services
                        .AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme =
                                TestIdentityService.SchemeName;
                            options.DefaultChallengeScheme =
                                TestIdentityService.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions,
                            TestIdentityService>(
                            TestIdentityService.SchemeName, _ => { });

                    services.AddScoped<IRealtimeNotificationService>(
                        _ => capture);
                }));

        return factory;
    }

    // ------------------------------------------------------------------
    // T3 — assignment push failure is non-fatal
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T3: a TaskAssigned push failure does not fail the committed " +
        "assignment")]
    public async Task AssignedPushFailure_ReturnsSuccess_WithCommittedAssignment()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedUnassignedTaskAsync(documentId);

        using var factory = BuildFactory(out var realtime);

        using var client = factory.CreateClient();

        realtime.ThrowOnAssignedPush = true;

        using var request = AssignAsAdminRequest(taskId, AgentA);

        var response = await client.SendAsync(request);

        // Pre-fix this is a 500: the throw escaped a committed assignment.
        await EnsureOk(response, HttpStatusCode.OK);

        Assert.True(realtime.AssignedPushAttempts > 0);

        // Pre-fix this is null: the response never came back, but the write
        // was already durable.
        Assert.Equal(AgentA, await ReadAssignedStaffIdAsync(taskId));
    }

    // ------------------------------------------------------------------
    // T4 — the guarded send creates no duplicate artifacts
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T4: a TaskAssigned push failure leaves exactly one history row and " +
        "one agent notification")]
    public async Task AssignedPushFailure_DoesNotDuplicateAssignmentArtifacts()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedUnassignedTaskAsync(documentId);

        using var factory = BuildFactory(out var realtime);

        using var client = factory.CreateClient();

        realtime.ThrowOnAssignedPush = true;

        using var request = AssignAsAdminRequest(taskId, AgentA);

        var response = await client.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);

        Assert.Equal(1, await CountHistoryAsync(taskId));

        Assert.Equal(1, await CountAssignmentNotificationsAsync(taskId));
    }

    // ------------------------------------------------------------------
    // Test double: faults ONLY SendTaskAssignedAsync
    // ------------------------------------------------------------------

    private sealed class FaultingAssignedPushRealtimeService
        : IRealtimeNotificationService
    {
        private int _assignedPushAttempts;

        public bool ThrowOnAssignedPush { get; set; }

        public int AssignedPushAttempts => _assignedPushAttempts;

        public Task SendTaskAssignedAsync(
            TaskAssignedEvent notification,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedPushAttempts);

            if (ThrowOnAssignedPush)
            {
                throw new InvalidOperationException(
                    "Injected TaskAssigned push failure.");
            }

            return Task.CompletedTask;
        }

        public Task SendNotificationReceivedAsync(
            Guid userId,
            NotificationReceivedDto notification,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendTaskStatusChangedAsync(
            TaskStatusChangedEvent notification,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
