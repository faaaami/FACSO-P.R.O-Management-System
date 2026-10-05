using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Admin;

/// <summary>
/// P10.6-C: the client approve/reject decision, its audit row and the
/// notification that tells the contact about it must commit as one unit.
///
/// These were previously two independent SaveChanges with no transaction between
/// them: the decision and the audit committed first, and only then did a second
/// save attempt the notification. A failure writing the notification therefore
/// left the account approved and audited with the notification lost for good —
/// and the idempotent early-return in the handler answers a retry with success
/// without ever producing it, so no retry could recover the gap either.
///
/// T1/T2 fail before the fix (the decision and audit survive) and pass after it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientApprovalNotificationAtomicityTests
    : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;

    /// <summary>
    /// The text of the fault thrown by the injected context. Asserted on the
    /// response body so a test can never pass because the request never reached
    /// the handler.
    /// </summary>
    private const string InjectedFailureMessage =
        "Injected notification persistence failure.";

    public ClientApprovalNotificationAtomicityTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private Task<HttpResponseMessage> PatchApprovalAsync(
        Guid contactId,
        bool approved)
        => ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/admin/clients/{CompanyA}/contacts/{contactId}/approval",
                new { approved },
                Admin,
                "Admin"));

    /// <summary>
    /// Creates a Client contact in CompanyA in the given account state. A
    /// freshly created user is used rather than a seeded one so notification and
    /// audit counts start at a known zero and cannot be perturbed by the mock
    /// seeder, which seeds the client inbox.
    ///
    /// The state matters because both handlers are idempotent by design and
    /// short-circuit before doing any work:
    /// approve returns early for an already-approved, already-active contact,
    /// and reject returns early for a contact that is neither approved nor
    /// active. Approve therefore needs a pending contact, while reject needs a
    /// currently-approved one - otherwise the test would only ever exercise the
    /// early return.
    /// </summary>
    private async Task<Guid> SeedContactAsync(
        bool isActive,
        bool isApproved)
    {
        var id = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.Users.Add(new User
            {
                Id = id,
                FullName = "Pending Contact",
                Email = $"pending-{Guid.NewGuid():N}@test.local",
                PasswordHash = "not-used",
                Role = UserRole.Client,
                IsActive = isActive,
                IsApproved = isApproved,
                EmailVerified = true,
                ClientCompanyId = CompanyA,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return id;
    }

    private async Task<(bool IsActive, bool IsApproved)> ReadStateAsync(
        Guid contactId)
    {
        (bool IsActive, bool IsApproved)? state = null;

        await WithScopeDbContextAsync(db =>
        {
            var user = db.Users.First(x => x.Id == contactId);

            state = (user.IsActive, user.IsApproved);
        });

        return state!.Value;
    }

    private async Task<int> CountAuditAsync(
        string action,
        Guid contactId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.AuditLogs.Count(x =>
                x.Action == action &&
                x.EntityId == contactId);
        });

        return count;
    }

    private async Task<int> CountNotificationsAsync(Guid contactId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.Notifications.Count(x => x.UserId == contactId);
        });

        return count;
    }

    /// <summary>
    /// Builds a host whose ApplicationDbContext refuses to save a notification
    /// insert while the fault is armed, and whose realtime service records every
    /// push.
    ///
    /// Arming happens after <c>CreateClient</c> so the derived host has started
    /// and run its startup seeding with the fault disarmed; seeding writes
    /// notifications of its own and must not be disturbed.
    /// </summary>
    private WebApplicationFactory<Program> BuildFactory(
        out NotificationSaveFault fault,
        out CapturingRealtimeNotificationService realtime)
    {
        var faultSwitch = new NotificationSaveFault();
        var capture = new CapturingRealtimeNotificationService();

        fault = faultSwitch;
        realtime = capture;

        return Factory.WithWebHostBuilder(
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

                    services.AddSingleton(faultSwitch);
                    services.AddSingleton(capture);

                    services.AddScoped<IRealtimeNotificationService>(
                        _ => capture);

                    services.AddScoped<ApplicationDbContext>(
                        sp => new NotificationFaultingDbContext(
                            sp.GetRequiredService<
                                DbContextOptions<ApplicationDbContext>>(),
                            sp.GetRequiredService<
                                NotificationSaveFault>()));
                }));
    }

    // ------------------------------------------------------------------
    // T1 — approve: notification failure rolls the whole decision back
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T1: approve with a failing notification insert leaves the contact " +
        "unapproved, unaudited and unnotified")]
    public async Task Approve_WhenNotificationInsertFails_RollsBackEverything()
    {
        var contactId = await SeedContactAsync(
            isActive: false,
            isApproved: false);

        using var factory = BuildFactory(
            out var fault,
            out var realtime);

        using var client = factory.CreateClient();

        // Arm only now: the host has started and seeded with the fault disarmed.
        fault.Arm();

        using var request = ScopePatch(
            $"/api/v1/admin/clients/{CompanyA}/contacts/{contactId}/approval",
            new { approved = true },
            Admin,
            "Admin");

        var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();

        Assert.NotEqual(
            HttpStatusCode.OK,
            response.StatusCode);

        // Proves the request reached the handler and the injected failure is
        // what answered it, so an auth or routing failure cannot make this test
        // pass vacuously.
        Assert.Contains(
            InjectedFailureMessage,
            body,
            StringComparison.Ordinal);

        // Nothing may survive. Before the fix the approval and the
        // ClientApproved audit row were already committed by the time the
        // notification insert was attempted, so these three assertions failed.
        var state = await ReadStateAsync(contactId);

        Assert.False(state.IsApproved);
        Assert.False(state.IsActive);

        Assert.Equal(
            0,
            await CountAuditAsync("ClientApproved", contactId));

        Assert.Equal(
            0,
            await CountNotificationsAsync(contactId));

        // The push is deliberately downstream of the commit, so a decision that
        // rolled back must never have been announced as approved.
        Assert.Empty(realtime.NotificationPushes);
    }

    // ------------------------------------------------------------------
    // T2 — reject: notification failure rolls the whole decision back
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T2: reject with a failing notification insert leaves the contact " +
        "active, unaudited and unnotified")]
    public async Task Reject_WhenNotificationInsertFails_RollsBackEverything()
    {
        // Seeded as a currently-approved contact: reject short-circuits for a
        // contact that is neither approved nor active, so a pending contact
        // would only exercise that early return.
        var contactId = await SeedContactAsync(
            isActive: true,
            isApproved: true);

        using var factory = BuildFactory(
            out var fault,
            out var realtime);

        using var client = factory.CreateClient();

        fault.Arm();

        using var request = ScopePatch(
            $"/api/v1/admin/clients/{CompanyA}/contacts/{contactId}/approval",
            new { approved = false },
            Admin,
            "Admin");

        var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();

        Assert.NotEqual(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Contains(
            InjectedFailureMessage,
            body,
            StringComparison.Ordinal);

        // Before the fix the deactivation and the ClientRejected audit row had
        // already been committed when the notification insert failed.
        var state = await ReadStateAsync(contactId);

        Assert.True(state.IsApproved);
        Assert.True(state.IsActive);

        Assert.Equal(
            0,
            await CountAuditAsync("ClientRejected", contactId));

        Assert.Equal(
            0,
            await CountNotificationsAsync(contactId));

        Assert.Empty(realtime.NotificationPushes);
    }

    // ------------------------------------------------------------------
    // T3 — happy path is unchanged
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T3: a successful approval commits the decision, audit and notification " +
        "together and pushes once")]
    public async Task Approve_WhenNotificationSucceeds_CommitsAllThreeAndPushesOnce()
    {
        var contactId = await SeedContactAsync(
            isActive: false,
            isApproved: false);

        using var factory = BuildFactory(
            out _,
            out var realtime);

        using var client = factory.CreateClient();

        using var request = ScopePatch(
            $"/api/v1/admin/clients/{CompanyA}/contacts/{contactId}/approval",
            new { approved = true },
            Admin,
            "Admin");

        var response = await client.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);

        var state = await ReadStateAsync(contactId);

        Assert.True(state.IsApproved);
        Assert.True(state.IsActive);

        Assert.Equal(
            1,
            await CountAuditAsync("ClientApproved", contactId));

        Assert.Equal(
            1,
            await CountNotificationsAsync(contactId));

        var push = Assert.Single(realtime.NotificationPushes);

        Assert.Equal(contactId, push.UserId);
    }

    // ------------------------------------------------------------------
    // Test doubles
    // ------------------------------------------------------------------

    /// <summary>
    /// Armable switch consulted by <see cref="NotificationFaultingDbContext"/>.
    /// Disarmed while the host seeds.
    /// </summary>
    private sealed class NotificationSaveFault
    {
        private int _armed;

        public bool IsArmed => Volatile.Read(ref _armed) == 1;

        public void Arm() => Interlocked.Exchange(ref _armed, 1);
    }

    /// <summary>
    /// An ApplicationDbContext that refuses to save once a notification is
    /// pending. This is the only way to reach the notification save failing
    /// without changing production code to accommodate the test: the message is
    /// a fixed literal inside the handler, so it cannot be pushed over the column
    /// limit from the outside.
    /// </summary>
    private sealed class NotificationFaultingDbContext(
        DbContextOptions options,
        NotificationSaveFault fault)
        : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (fault.IsArmed)
            {
                var carriesNotification =
                    ChangeTracker
                        .Entries<Notification>()
                        .Any(e => e.State == EntityState.Added);

                if (carriesNotification)
                {
                    throw new InvalidOperationException(
                        InjectedFailureMessage);
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Records notification pushes. Shares the shape of the fixture's
    /// NoOpRealtimeNotificationService, but keeps the evidence the happy path
    /// needs: exactly one push, to the contact that was decided.
    /// </summary>
    private sealed class CapturingRealtimeNotificationService
        : IRealtimeNotificationService
    {
        private readonly ConcurrentQueue<(Guid UserId, NotificationReceivedDto Payload)>
            _notificationPushes = new();

        public IReadOnlyList<(Guid UserId, NotificationReceivedDto Payload)>
            NotificationPushes => _notificationPushes.ToArray();

        public Task SendNotificationReceivedAsync(
            Guid userId,
            NotificationReceivedDto notification,
            CancellationToken cancellationToken)
        {
            _notificationPushes.Enqueue((userId, notification));

            return Task.CompletedTask;
        }

        public Task SendTaskAssignedAsync(
            TaskAssignedEvent notification,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendTaskStatusChangedAsync(
            TaskStatusChangedEvent notification,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
