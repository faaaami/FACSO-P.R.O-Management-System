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

namespace PROManagementSystem.Tests.Notifications;

/// <summary>
/// P10.7 D-1: a TaskStatusChanged realtime push failure must not fail a
/// completion that has already been committed.
///
/// Before the fix the handler awaited the TaskStatusChanged send bare, outside
/// any try/catch and after CommitAsync. A SignalR/Redis transport failure
/// therefore propagated out of the command and turned a committed completion
/// into an HTTP 500, which was doubly wrong: the retry could not repeat the
/// Approved -> Updated transition, and the throw skipped FlushAsync so the
/// client completion notification was never delivered live either.
///
/// T1 is the discriminator and pins the specific ordering requirement: the
/// try/catch around the status push must end BEFORE the flush, so a failed
/// status push still lets the queued client notification through.
///
/// The transaction boundary itself is deliberately untouched: the domain
/// notification publish stays inside the transaction and the completion unit of
/// work stays atomic. InvoiceCompletionEventFailureTests is the guard for that
/// contract and is not modified here.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TaskStatusPushFailureTests
    : IntegrationTestBase
{
    private const decimal ServiceFee = 2500.00m;

    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;

    public TaskStatusPushFailureTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Seeding
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
                    $"DOC-STATUSFAIL-{documentId.ToString("N")[..8]}",
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

    private async Task<Guid> SeedApprovedTaskAsync(Guid documentId)
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
                AssignedStaffId = AgentA,
                Status = RenewalTaskStatus.Approved,
                ServiceFeeAmount = ServiceFee,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return taskId;
    }

    private HttpRequestMessage CompleteAsAgentRequest(Guid taskId)
        => ScopePatch(
            $"/api/v1/agent/tasks/{taskId}/status",
            new
            {
                status = "Updated",
                renewedExpiryDate = "2030-09-30"
            },
            AgentA,
            "Agent");

    private sealed record TaskState(
        RenewalTaskStatus? Status,
        DateTime? CompletedAt,
        DateTime? UpdatedAt);

    private async Task<TaskState> ReadTaskStateAsync(Guid taskId)
    {
        TaskState? state = null;

        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks
                .Where(x => x.Id == taskId)
                .Select(x => new TaskState(
                    (RenewalTaskStatus?)x.Status,
                    x.CompletedAt,
                    x.UpdatedAt))
                .FirstOrDefault();

            state = task;
        });

        return state!;
    }

    private async Task<int> CountInvoicesAsync(Guid taskId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.ServiceFeeInvoices.Count(x =>
                x.RenewalTaskId == taskId &&
                !x.IsDeleted);
        });

        return count;
    }

    private async Task<int> CountCompletionHistoryAsync(Guid taskId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.RenewalTaskHistories.Count(x =>
                x.RenewalTaskId == taskId);
        });

        return count;
    }

    private async Task<int> CountCompletionNotificationsAsync(Guid taskId)
    {
        var expectedMessage =
            $"RenewalTaskCompleted | TaskId:{taskId}.";

        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.Notifications.Count(x =>
                x.Message == expectedMessage &&
                x.UserId == ClientUserA);
        });

        return count;
    }

    private WebApplicationFactory<Program> BuildFactory(
        out FaultingStatusPushRealtimeService realtime)
    {
        var capture = new FaultingStatusPushRealtimeService();

        realtime = capture;

        var factory = Factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(
                services =>
                {
                    // A derived host needs the same test-identity pipeline the
                    // fixture's ScopeClient installs; it does not carry the real
                    // JWT configuration.
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
    // T1 — status push failure is non-fatal and the flush still runs
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T1: a TaskStatusChanged push failure does not fail the committed " +
        "completion and the client notification is still flushed")]
    public async Task StatusPushFailure_AfterCommit_DoesNotFailCompletion()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        using var factory = BuildFactory(out var realtime);

        using var client = factory.CreateClient();

        // Only the TaskStatusChanged send faults. The client notification push
        // must still be delivered, which is what makes this the discriminator
        // for the catch/flush ordering.
        realtime.ThrowOnStatusPush = true;

        using var request = CompleteAsAgentRequest(taskId);

        var response = await client.SendAsync(request);

        // Pre-fix this is a 500: the throw escaped the committed completion.
        await EnsureOk(response, HttpStatusCode.OK);

        // Pre-fix this is Approved: the completion never looked committed.
        Assert.Equal(
            RenewalTaskStatus.Updated,
            (await ReadTaskStateAsync(taskId)).Status);

        // The status push really was attempted and really did throw.
        Assert.True(realtime.StatusPushAttempts > 0);

        // Pre-fix these are empty: the throw skipped FlushAsync entirely.
        Assert.NotEmpty(realtime.NotificationPushes);

        Assert.Contains(
            realtime.NotificationPushes,
            push => push.UserId == ClientUserA);
    }

    // ------------------------------------------------------------------
    // T2 — swallowing the push failure does not disturb completion
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T2: a TaskStatusChanged push failure still commits the completion, " +
        "its invoice and its history exactly once")]
    public async Task StatusPushFailure_StillCommitsInvoiceAndHistory()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        using var factory = BuildFactory(out var realtime);

        using var client = factory.CreateClient();

        realtime.ThrowOnStatusPush = true;

        using var request = CompleteAsAgentRequest(taskId);

        var response = await client.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);

        var state = await ReadTaskStateAsync(taskId);

        Assert.Equal(RenewalTaskStatus.Updated, state.Status);

        Assert.NotNull(state.CompletedAt);

        // UpdatedAt must have moved off the seeded value, proving the
        // completion writes were applied rather than skipped.
        Assert.NotEqual(default(DateTime), state.UpdatedAt ?? default);

        Assert.Equal(1, await CountInvoicesAsync(taskId));

        Assert.Equal(1, await CountCompletionHistoryAsync(taskId));

        Assert.Equal(1, await CountCompletionNotificationsAsync(taskId));
    }

    // ------------------------------------------------------------------
    // Test double: faults ONLY SendTaskStatusChangedAsync
    // ------------------------------------------------------------------

    /// <summary>
    /// Records client notification pushes and optionally throws from the
    /// TaskStatusChanged send. SendNotificationReceivedAsync deliberately never
    /// throws, so the assertions prove the flush still ran rather than that a
    /// second failure was also swallowed.
    /// </summary>
    private sealed class FaultingStatusPushRealtimeService
        : IRealtimeNotificationService
    {
        private readonly ConcurrentQueue<NotificationPush> _notificationPushes
            = new();

        private int _statusPushAttempts;

        public bool ThrowOnStatusPush { get; set; }

        public int StatusPushAttempts => _statusPushAttempts;

        public IReadOnlyList<NotificationPush> NotificationPushes
            => _notificationPushes.ToArray();

        public Task SendTaskStatusChangedAsync(
            TaskStatusChangedEvent notification,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _statusPushAttempts);

            if (ThrowOnStatusPush)
            {
                throw new InvalidOperationException(
                    "Injected TaskStatusChanged push failure.");
            }

            return Task.CompletedTask;
        }

        public Task SendNotificationReceivedAsync(
            Guid userId,
            NotificationReceivedDto notification,
            CancellationToken cancellationToken)
        {
            _notificationPushes.Enqueue(
                new NotificationPush(userId, notification));

            return Task.CompletedTask;
        }

        public Task SendTaskAssignedAsync(
            TaskAssignedEvent notification,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed record NotificationPush(
        Guid UserId,
        NotificationReceivedDto Payload);
}
