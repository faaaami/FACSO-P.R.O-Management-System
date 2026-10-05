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
/// P10.6-B: the client completion notification must be pushed only after the
/// task-completion transaction has committed.
///
/// Before the fix the completion event handler pushed to SignalR from inside the
/// transaction (the publish stays inside it so the notification rows and the
/// service fee invoice commit atomically). A client could therefore be told a
/// renewal task was completed and then have the completion roll back.
///
/// T1 asserts the ordering directly and is the discriminator: at the moment the
/// push happens, an independent READ COMMITTED connection must already see the
/// task as Updated. Pre-fix it sees the previous status (Approved) and fails.
///
/// The publish is deliberately left inside the transaction by the fix; the
/// invoice/completion atomicity contract is guarded by
/// InvoiceCompletionEventFailureTests and is not touched here.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class NotificationPushOrderingTests
    : IntegrationTestBase
{
    private const decimal ServiceFee = 2500.00m;

    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;

    public NotificationPushOrderingTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Seeding: a fresh Approved task so the test never races shared state
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
                    $"DOC-PUSH-{documentId.ToString("N")[..8]}",
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

    private async Task<RenewalTaskStatus?> ReadTaskStatusAsync(Guid taskId)
    {
        RenewalTaskStatus? status = null;

        await WithScopeDbContextAsync(db =>
        {
            status = db.RenewalTasks
                .Where(x => x.Id == taskId)
                .Select(x => (RenewalTaskStatus?)x.Status)
                .FirstOrDefault();
        });

        return status;
    }

    private WebApplicationFactory<Program> BuildFactory(
        out RecordingRealtimeNotificationService realtime)
    {
        var capture = new RecordingRealtimeNotificationService();

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

        // The recording service opens its own scope per push to read committed
        // state, so it needs the running provider.
        capture.Services = factory.Services;

        return factory;
    }

    // ------------------------------------------------------------------
    // T1 — the push observes committed state (primary discriminator)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T1: the client completion push happens only after the task status is " +
        "committed as Updated")]
    public async Task CompletionPush_ObservesCommittedUpdatedStatus()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        using var factory = BuildFactory(out var realtime);

        using var client = factory.CreateClient();

        using var request = CompleteAsAgentRequest(taskId);

        var response = await client.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);

        // At least one client of CompanyA was notified, and every push observed
        // the task as already committed Updated.
        Assert.NotEmpty(realtime.NotificationPushes);

        Assert.Contains(
            realtime.NotificationPushes,
            push => push.UserId == ClientUserA);

        Assert.All(
            realtime.NotificationPushes,
            push => Assert.Equal(
                RenewalTaskStatus.Updated,
                push.TaskStatusObservedAtPush));
    }

    // ------------------------------------------------------------------
    // T2 — a push failure after commit is non-fatal
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T2: a realtime failure during the post-commit flush does not fail the " +
        "already-committed completion")]
    public async Task FlushFailure_AfterCommit_DoesNotFailCompletion()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        using var factory = BuildFactory(out var realtime);

        realtime.ThrowOnNotificationPush = true;

        using var client = factory.CreateClient();

        using var request = CompleteAsAgentRequest(taskId);

        var response = await client.SendAsync(request);

        // The domain operation must succeed even though the deferred push threw.
        await EnsureOk(response, HttpStatusCode.OK);

        Assert.Equal(
            RenewalTaskStatus.Updated,
            await ReadTaskStatusAsync(taskId));

        Assert.Equal(1, await CountNotificationsAsync(taskId));
    }

    private async Task<int> CountNotificationsAsync(Guid taskId)
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

    // ------------------------------------------------------------------
    // Test double
    // ------------------------------------------------------------------

    private sealed record PushObservation(
        Guid UserId,
        RenewalTaskStatus? TaskStatusObservedAtPush);

    /// <summary>
    /// Records every client completion push and, at the instant it is invoked,
    /// reads the task status through an independent scope/connection. That read
    /// is the ordering probe: a push issued while the transaction is open cannot
    /// see the uncommitted Updated status, whereas a post-commit push can.
    /// </summary>
    private sealed class RecordingRealtimeNotificationService
        : IRealtimeNotificationService
    {
        private readonly ConcurrentQueue<PushObservation> _notificationPushes
            = new();

        public IServiceProvider Services { get; set; } = null!;

        public bool ThrowOnNotificationPush { get; set; }

        public IReadOnlyList<PushObservation> NotificationPushes
            => _notificationPushes.ToArray();

        public async Task SendNotificationReceivedAsync(
            Guid userId,
            NotificationReceivedDto notification,
            CancellationToken cancellationToken)
        {
            // RenewalTaskCompleted | TaskId:{id}.
            var taskId = ParseTaskId(notification.Message);

            _notificationPushes.Enqueue(
                new PushObservation(
                    userId,
                    ReadStatusAtThisInstant(taskId)));

            if (ThrowOnNotificationPush)
            {
                throw new InvalidOperationException(
                    "Injected realtime push failure.");
            }

            await Task.CompletedTask;
        }

        public Task SendTaskAssignedAsync(
            TaskAssignedEvent notification,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendTaskStatusChangedAsync(
            TaskStatusChangedEvent notification,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        private RenewalTaskStatus? ReadStatusAtThisInstant(Guid taskId)
        {
            if (taskId == Guid.Empty)
            {
                return null;
            }

            using var scope = Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            return db.RenewalTasks
                .Where(x => x.Id == taskId)
                .Select(x => (RenewalTaskStatus?)x.Status)
                .FirstOrDefault();
        }

        private static Guid ParseTaskId(string message)
        {
            const string marker = "TaskId:";

            var start = message.IndexOf(
                marker,
                StringComparison.Ordinal);

            if (start < 0)
            {
                return Guid.Empty;
            }

            start += marker.Length;

            var end = message.IndexOf('.', start);

            if (end < 0)
            {
                end = message.Length;
            }

            return Guid.TryParse(
                message[start..end],
                out var id)
                ? id
                : Guid.Empty;
        }
    }
}
