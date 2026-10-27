using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Seeders;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.ServiceRequests;

/// <summary>
/// The Admin decision on a service request: convert it into a renewal task, or
/// reject it with a reason.
///
/// What this file pins down, beyond what the earlier tenancy tests already cover:
///
///  - CONVERSION IS ATOMIC. The renewal task and the request's own Converted
///    transition are one decision and share one transaction. Every failure case
///    here asserts the same thing: the request is STILL SUBMITTED and NO TASK
///    EXISTS. A conversion that cannot complete must leave nothing behind,
///    because a task without its conversion used to strand the request in
///    Submitted with the retry blocked by the active-task guard.
///
///  - THE DOCUMENT MUST BELONG TO THE REQUEST'S COMPANY. Task creation resolves
///    its own company from the document, so an inconsistent link would quietly
///    file the task under the wrong tenant.
///
///  - A REJECTION REASON MUST CONTAIN SOMETHING. A whitespace-only reason is
///    refused rather than stored as an empty string, because a client shown a
///    rejection with no explanation has been told nothing.
///
///  - REJECTION IS ATOMIC AND VALIDATED BEFORE THE WRITE.
///
///  - THE TWO DECISIONS ARE MUTUALLY EXCLUSIVE UNDER CONCURRENCY. Convert and
///    reject take the same row lock, so exactly one can be applied and a
///    request can never end up Converted while also carrying a reason.
///
///  - A DECISION NOTIFIES THE RIGHT AUDIENCE. A new request reaches the admins
///    who can decide it; a decision reaches the client company, carrying the
///    reason.
///
///  - ONLY AN ADMIN MAY DECIDE.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ServiceRequestDecisionTests : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid ClientA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid ClientB = TestDataSeeder.Ids.ClientUserB;

    private static readonly string CompanyA =
        TestDataSeeder.Ids.CompanyA.ToString();

    private static readonly string CompanyB =
        TestDataSeeder.Ids.CompanyB.ToString();

    /// <summary>
    /// A company with no pre-seeded service requests, and a subject whose visa is
    /// free of an active task. The shared seed already plants a SUBMITTED
    /// EarlyRenewal for CompanyA + EmployeeA, which the create handler's
    /// duplicate-open-request guard refuses, and the mock seed plants an active
    /// task on Ahmed's visa, which the active-task guard in renewal-task creation
    /// refuses. Omar has neither, so any test that needs the public create endpoint
    /// and a successful conversion uses this subject.
    /// </summary>
    private static readonly Guid MockCompanyId =
        MockClientCompanySeeder.Ids.Company;

    private static readonly Guid MockCompanyClient =
        MockClientCompanySeeder.Ids.Client;

    private static readonly Guid MockCompanyEmployee =
        MockClientCompanySeeder.Ids.EmployeeOmar;

    public ServiceRequestDecisionTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private sealed record TestResponse<T>(bool Success, int StatusCode, T? Data);

    private sealed record ConvertResponseDto(
        Guid ServiceRequestId,
        Guid RenewalTaskId,
        ServiceRequestStatus Status,
        DateTime ConvertedAt);

    private sealed record RejectResponseDto(
        Guid ServiceRequestId,
        ServiceRequestStatus Status,
        string RejectionReason,
        DateTime RejectedAt);

    /// <summary>
    /// CreateServiceRequestResponseDto names its id `Id`, not
    /// `ServiceRequestId` — the two decision responses use the longer name, so
    /// mixing them up silently yields an empty Guid here and a 400 from the
    /// validator on the far side.
    /// </summary>
    private sealed record CreateResponseDto(
        Guid Id,
        Guid ClientCompanyId,
        ServiceRequestType Type,
        Guid? EmployeeId,
        Guid? EntityId,
        Guid? DocumentId,
        ServiceRequestStatus Status,
        string? Description,
        DateTime CreatedAt);

    /// <summary>
    /// Plants an active Visa document, which is what
    /// CreateServiceRequestCommandHandler resolves for an EarlyRenewal. A fresh id
    /// and document number are used on every call so the filtered unique index on
    /// an active task's DocumentId can never collide with a seeded row.
    /// </summary>
    private async Task<Guid> SeedActiveVisaAsync(
        Guid? employeeId = null,
        Guid? entityId = null)
    {
        var documentId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = employeeId,
                ClientEntityId = entityId,
                Type = DocumentType.Visa,
                DocumentNumber = $"VISA-{documentId.ToString("N")[..8]}",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(2),
                FileUrl = $"https://storage.test/{documentId}",
                FileName = "visa.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return documentId;
    }

    /// <summary>
    /// Creates a request straight in the database, for the cases that need a
    /// specific state the public API cannot produce (a request with no linked
    /// document, or one whose document belongs to another company).
    /// </summary>
    private async Task<Guid> SeedServiceRequestAsync(
        Guid? documentId,
        Guid? employeeId = null,
        Guid? entityId = null,
        ServiceRequestType type = ServiceRequestType.Other,
        Guid? clientCompanyId = null)
    {
        var serviceRequestId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = serviceRequestId,
                ClientCompanyId = clientCompanyId ??
                    TestDataSeeder.Ids.CompanyA,
                Type = type,
                EmployeeId = employeeId ?? TestDataSeeder.Ids.EmployeeA,
                EntityId = entityId,
                DocumentId = documentId,
                Status = ServiceRequestStatus.Submitted,
                Description = "Decision test request.",
                RejectionReason = null,
                ConvertedAt = null,
                RejectedAt = null,
                ConvertedRenewalTaskId = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return serviceRequestId;
    }

    private async Task<ServiceRequest?> LoadRequestAsync(Guid id)
    {
        ServiceRequest? loaded = null;

        await WithScopeDbContextAsync(db =>
        {
            loaded = db.ServiceRequests
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == id);
        });

        return loaded;
    }

    private async Task<int> TaskCountForDocumentAsync(Guid documentId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.RenewalTasks.Count(x =>
                x.DocumentId == documentId && !x.IsDeleted);
        });

        return count;
    }

    private async Task<int> AllTaskCountAsync()
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.RenewalTasks.Count(x => !x.IsDeleted);
        });

        return count;
    }

    private async Task<RenewalTask?> LoadTaskAsync(Guid id)
    {
        RenewalTask? loaded = null;

        await WithScopeDbContextAsync(db =>
        {
            loaded = db.RenewalTasks
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == id);
        });

        return loaded;
    }

    private async Task<int> AuditCountAsync(string action)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.AuditLogs.Count(x => x.Action == action);
        });

        return count;
    }

    private async Task<List<(Guid UserId, string Title, string Message)>>
        NotificationsForAsync(Guid userId)
    {
        var rows = new List<(Guid, string, string)>();

        await WithScopeDbContextAsync(db =>
        {
            rows = db.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .Select(x => new ValueTuple<Guid, string, string>(
                    x.UserId, x.Title, x.Message))
                .ToList();
        });

        return rows;
    }

    private Task<HttpResponseMessage> AdminConvertAsync(
        Guid serviceRequestId)
        => ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{serviceRequestId}/convert",
                null,
                Admin, role: "Admin"));

    private Task<HttpResponseMessage> AdminRejectAsync(
        Guid serviceRequestId,
        string? reason)
        => ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{serviceRequestId}/reject",
                reason is null ? new { } : new { reason },
                Admin, role: "Admin"));

    // ==================================================================
    // Conversion: the happy path, and exactly what it produces
    // ==================================================================

    [Fact(DisplayName =
        "P6-C1: converting an early renewal creates an unassigned, unfee'd Submitted task")]
    public async Task Convert_ProducesUnassignedUnfeeSubmittedTask()
    {
        // The full public path: the client raises an EarlyRenewal, which resolves
        // its own document, and the admin converts it.
        //
        // This runs against the mock company rather than CompanyA, because the
        // shared seed already plants a SUBMITTED EarlyRenewal for CompanyA +
        // EmployeeA, and the create handler's duplicate-open-request guard would
        // answer 409 before the workflow under test was ever reached.
        var created = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "EarlyRenewal",
                    employeeId = MockCompanyEmployee.ToString(),
                    notes = "Visa renews early this quarter."
                },
                MockCompanyClient, role: "Client",
                clientCompanyId: MockCompanyId.ToString()));

        await EnsureOk(created, HttpStatusCode.Created);

        var createdBody =
            await ReadJsonAsync<TestResponse<CreateResponseDto>>(created);

        var serviceRequestId = createdBody!.Data!.Id;

        // The document was resolved by the create handler, so conversion is possible.
        Assert.NotNull(createdBody.Data.DocumentId);

        var response = await AdminConvertAsync(serviceRequestId);

        await EnsureOk(response, HttpStatusCode.OK);

        var body =
            await ReadJsonAsync<TestResponse<ConvertResponseDto>>(response);

        Assert.Equal(
            ServiceRequestStatus.Converted, body!.Data!.Status);

        var task = await LoadTaskAsync(body.Data.RenewalTaskId);

        Assert.NotNull(task);

        // Exactly what CreateRenewalTaskCommandHandler writes. A conversion is not
        // an assignment, a fee, or a step in the task lifecycle, and a test that
        // let any of those drift would be the only thing standing between the UI's
        // wording and reality.
        Assert.Equal(RenewalTaskStatus.Submitted, task!.Status);
        Assert.Null(task.AssignedStaffId);
        Assert.Null(task.ServiceFeeAmount);
        Assert.Null(task.CompletedAt);
        Assert.Equal(MockCompanyId, task.ClientCompanyId);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(
            ServiceRequestStatus.Converted, request!.Status);
        Assert.Equal(task.Id, request.ConvertedRenewalTaskId);
        Assert.NotNull(request.ConvertedAt);
        Assert.Null(request.RejectionReason);
    }

    [Fact(DisplayName =
        "P6-C2: converting writes the renewal task history and the decision audit")]
    public async Task Convert_WritesTaskHistoryAndAudit()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        var response = await AdminConvertAsync(serviceRequestId);

        await EnsureOk(response, HttpStatusCode.OK);

        var body =
            await ReadJsonAsync<TestResponse<ConvertResponseDto>>(response);

        var historyCount = 0;

        await WithScopeDbContextAsync(db =>
        {
            historyCount = db.RenewalTaskHistories.Count(x =>
                x.RenewalTaskId == body!.Data!.RenewalTaskId);
        });

        Assert.Equal(1, historyCount);
        Assert.Equal(1, await AuditCountAsync("ServiceRequestConverted"));
    }

    [Fact(DisplayName =
        "P6-C3: a converted request cannot be converted again (409)")]
    public async Task Convert_Twice_Returns409()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        await EnsureOk(await AdminConvertAsync(serviceRequestId));

        var second = await AdminConvertAsync(serviceRequestId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // The refusal created nothing.
        Assert.Equal(1, await TaskCountForDocumentAsync(documentId));
    }

    // ==================================================================
    // Conversion failures: in every case, NOTHING is written
    // ==================================================================

    [Fact(DisplayName =
        "P6-C4: converting a request with no linked document is 409 and creates no task")]
    public async Task Convert_NoDocument_Returns409_NoTask()
    {
        var serviceRequestId =
            await SeedServiceRequestAsync(documentId: null);

        // The shared seed already plants renewal tasks, so the assertion is that
        // this call ADDS none rather than that none exist. A refused conversion
        // that still created a task is exactly the half-finished state the
        // transaction exists to prevent.
        var tasksBefore = await AllTaskCountAsync();

        var response = await AdminConvertAsync(serviceRequestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
        Assert.Null(request.ConvertedRenewalTaskId);

        Assert.Equal(tasksBefore, await AllTaskCountAsync());
        Assert.Equal(0, await AuditCountAsync("ServiceRequestConverted"));
    }

    [Fact(DisplayName =
        "P6-C5: converting a deactivated document leaves the request submitted and creates no task")]
    public async Task Convert_InactiveDocument_LeavesNothingBehind()
    {
        // This is the atomicity case. CreateRenewalTaskCommandHandler refuses an
        // inactive document, and it refuses AFTER the request has been accepted.
        // Before the two writes shared a transaction that left a renewal task
        // behind for a request still reading Submitted, with the retry then
        // blocked by the active-task guard.
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents.First(x => x.Id == documentId);
            document.IsActive = false;
            db.SaveChanges();
        });

        var response = await AdminConvertAsync(serviceRequestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
        Assert.Null(request.ConvertedRenewalTaskId);
        Assert.Null(request.ConvertedAt);

        Assert.Equal(0, await TaskCountForDocumentAsync(documentId));
    }

    [Fact(DisplayName =
        "P6-C6: converting when the document already has an active task is 409 and creates no second task")]
    public async Task Convert_ExistingActiveTask_Returns409_NoSecondTask()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        await WithScopeDbContextAsync(db =>
        {
            db.RenewalTasks.Add(new RenewalTask
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = null,
                Status = RenewalTaskStatus.Submitted,
                ServiceFeeAmount = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        var response = await AdminConvertAsync(serviceRequestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
        Assert.Equal(1, await TaskCountForDocumentAsync(documentId));
    }

    [Fact(DisplayName =
        "P6-C7: a document belonging to another company cannot be converted (409) and no task is filed")]
    public async Task Convert_CrossCompanyDocument_Returns409_NoTask()
    {
        // EmployeeB belongs to company B. Task creation resolves the task's company
        // from the DOCUMENT, not from the request, so without an explicit check
        // this would create a real renewal task against company B's data in
        // response to company A's request.
        var foreignDocumentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeB);

        var serviceRequestId = await SeedServiceRequestAsync(
            foreignDocumentId,
            employeeId: TestDataSeeder.Ids.EmployeeA,
            clientCompanyId: TestDataSeeder.Ids.CompanyA);

        var response = await AdminConvertAsync(serviceRequestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
        Assert.Equal(0, await TaskCountForDocumentAsync(foreignDocumentId));
    }

    [Fact(DisplayName =
        "P6-C8: converting an unknown request is 404")]
    public async Task Convert_Unknown_Returns404()
    {
        var response = await AdminConvertAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ==================================================================
    // Rejection
    // ==================================================================

    [Fact(DisplayName =
        "P6-R1: rejecting stores the reason trimmed, with a timestamp")]
    public async Task Reject_StoresTrimmedReason()
    {
        var serviceRequestId = await SeedServiceRequestAsync(null);

        var response = await AdminRejectAsync(
            serviceRequestId, "   Duplicate of an existing request.   ");

        await EnsureOk(response, HttpStatusCode.OK);

        var body =
            await ReadJsonAsync<TestResponse<RejectResponseDto>>(response);

        Assert.Equal(
            ServiceRequestStatus.Rejected, body!.Data!.Status);
        Assert.Equal(
            "Duplicate of an existing request.",
            body.Data.RejectionReason);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Rejected, request!.Status);
        Assert.Equal(
            "Duplicate of an existing request.",
            request.RejectionReason);
        Assert.NotNull(request.RejectedAt);
        Assert.Null(request.ConvertedRenewalTaskId);
        Assert.Equal(1, await AuditCountAsync("ServiceRequestRejected"));
    }

    [Theory(DisplayName =
        "P6-R2: a reason with no real content is refused (400)")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public async Task Reject_BlankReason_Returns400(string reason)
    {
        // NotEmpty() alone accepts whitespace, and the handler then trims it to an
        // empty string — a rejection the client is shown with no explanation.
        var serviceRequestId = await SeedServiceRequestAsync(null);

        var response = await AdminRejectAsync(serviceRequestId, reason);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        // Refused BEFORE any write: still submitted, still undecided.
        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
        Assert.Null(request.RejectionReason);
        Assert.Null(request.RejectedAt);
        Assert.Equal(0, await AuditCountAsync("ServiceRequestRejected"));
    }

    [Fact(DisplayName =
        "P6-R3: a reason over 1000 characters is refused (400)")]
    public async Task Reject_OverlongReason_Returns400()
    {
        var serviceRequestId = await SeedServiceRequestAsync(null);

        var response = await AdminRejectAsync(
            serviceRequestId, new string('x', 1001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
    }

    [Fact(DisplayName =
        "P6-R4: a reason of exactly 1000 characters is accepted")]
    public async Task Reject_ReasonAtLimit_Succeeds()
    {
        // The boundary is a real one, so the edge is pinned from both sides.
        var serviceRequestId = await SeedServiceRequestAsync(null);

        var reason = new string('x', 1000);

        var response = await AdminRejectAsync(serviceRequestId, reason);

        await EnsureOk(response, HttpStatusCode.OK);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(reason, request!.RejectionReason);
    }

    [Fact(DisplayName =
        "P6-R5: a rejected request cannot be rejected again (409)")]
    public async Task Reject_Twice_Returns409()
    {
        var serviceRequestId = await SeedServiceRequestAsync(null);

        await EnsureOk(
            await AdminRejectAsync(serviceRequestId, "First reason."));

        var second = await AdminRejectAsync(
            serviceRequestId, "Second reason.");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // The first reason stands; the second did not overwrite it.
        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal("First reason.", request!.RejectionReason);
        Assert.Equal(1, await AuditCountAsync("ServiceRequestRejected"));
    }

    [Fact(DisplayName =
        "P6-R6: a converted request cannot be rejected (409)")]
    public async Task Reject_AfterConvert_Returns409()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        await EnsureOk(await AdminConvertAsync(serviceRequestId));

        var response = await AdminRejectAsync(
            serviceRequestId, "Too late.");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // The decisive assertion: a converted request must never also carry a
        // rejection reason.
        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Converted, request!.Status);
        Assert.Null(request.RejectionReason);
        Assert.Null(request.RejectedAt);
    }

    [Fact(DisplayName =
        "P6-R7: rejecting an unknown request is 404")]
    public async Task Reject_Unknown_Returns404()
    {
        var response = await AdminRejectAsync(
            Guid.NewGuid(), "No such request.");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "P6-R8: a rejection with no reason in the body is refused (400), not a 500")]
    public async Task Reject_MissingReason_Returns400()
    {
        // The body DTO takes a non-nullable string, so an omitted reason can
        // arrive as null. The validator has to stop it before the handler trims it,
        // or this is a NullReferenceException surfaced as a server error.
        var serviceRequestId = await SeedServiceRequestAsync(null);

        var response = await AdminRejectAsync(
            serviceRequestId, reason: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
    }

    [Fact(DisplayName =
        "P6-R9: a 1000-character reason is stored whole and the rejection succeeds")]
    public async Task Reject_MaxLengthReason_Succeeds_AndAuditStaysInWidth()
    {
        // Regression. AuditLog.Description is nvarchar(500) and the reason is valid
        // to 1000 characters, so quoting the reason in full in the audit entry
        // overflowed the column and surfaced as a 500. The rejection was rolled
        // back and the admin was told a valid request could not be rejected at all.
        var serviceRequestId = await SeedServiceRequestAsync(null);

        var reason = new string('r', 1000);

        var response = await AdminRejectAsync(serviceRequestId, reason);

        await EnsureOk(response, HttpStatusCode.OK);

        // The authoritative copy keeps the reason whole.
        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(reason, request!.RejectionReason);

        // And the audit entry is a bounded preview that fits its column, so the
        // audit log still records why.
        string? auditDescription = null;

        await WithScopeDbContextAsync(db =>
        {
            auditDescription = db.AuditLogs
                .AsNoTracking()
                .Where(x => x.Action == "ServiceRequestRejected")
                .Select(x => x.Description)
                .FirstOrDefault();
        });

        Assert.NotNull(auditDescription);
        Assert.StartsWith("Service request was rejected: ", auditDescription);
        Assert.True(
            auditDescription!.Length <= 500,
            $"Audit description was {auditDescription.Length} characters.");
    }

    // ==================================================================
    // Concurrency: the two decisions are mutually exclusive
    // ==================================================================

    [Fact(DisplayName =
        "P6-X1: a concurrent convert and reject apply exactly one decision")]
    public async Task ConvertAndReject_Concurrently_ExactlyOneWins()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        // Both requests are issued together. Whether they actually interleave
        // inside the handlers is not guaranteed, and the test does not depend on
        // it: the invariant under test is that the two decisions are serialised,
        // so exactly one succeeds and the stored record is never a mixture.
        var convertTask = AdminConvertAsync(serviceRequestId);
        var rejectTask = AdminRejectAsync(
            serviceRequestId, "Racing rejection.");

        await Task.WhenAll(convertTask, rejectTask);

        var convertResponse = await convertTask;
        var rejectResponse = await rejectTask;

        var succeeded = new[]
        {
            convertResponse.StatusCode,
            rejectResponse.StatusCode
        }.Count(s => s == HttpStatusCode.OK);

        Assert.Equal(1, succeeded);

        var request = await LoadRequestAsync(serviceRequestId);

        if (request!.Status == ServiceRequestStatus.Converted)
        {
            Assert.Equal(1, await TaskCountForDocumentAsync(documentId));
            Assert.Null(request.RejectionReason);
            Assert.Null(request.RejectedAt);
        }
        else
        {
            Assert.Equal(ServiceRequestStatus.Rejected, request.Status);
            Assert.Equal("Racing rejection.", request.RejectionReason);
            Assert.Equal(0, await TaskCountForDocumentAsync(documentId));
        }
    }

    [Fact(DisplayName =
        "P6-X2: two concurrent conversions create exactly one task and one conversion")]
    public async Task ConvertTwice_Concurrently_OneTask()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        var first = AdminConvertAsync(serviceRequestId);
        var second = AdminConvertAsync(serviceRequestId);

        await Task.WhenAll(first, second);

        var responses = new[] { await first, await second };

        Assert.Equal(
            1,
            responses.Count(r => r.StatusCode == HttpStatusCode.OK));

        // The uniqueness the database enforces for ordinary task creation is doing
        // real work here, and the losing request must not have left a task.
        Assert.Equal(1, await TaskCountForDocumentAsync(documentId));
        Assert.Equal(
            1, await AuditCountAsync("ServiceRequestConverted"));
    }

    // ==================================================================
    // Notifications
    // ==================================================================

    [Fact(DisplayName =
        "P6-N1: a new request notifies the admin who can decide it")]
    public async Task NewRequest_NotifiesAdmin()
    {
        var created = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "EarlyRenewal",
                    employeeId = MockCompanyEmployee.ToString(),
                    notes = "Please advise on an early renewal."
                },
                MockCompanyClient, role: "Client",
                clientCompanyId: MockCompanyId.ToString()));

        await EnsureOk(created, HttpStatusCode.Created);

        var adminNotifications = await NotificationsForAsync(Admin);

        // Plain language for the admin: who asked for what, never raw ids.
        Assert.Contains(adminNotifications, n =>
            n.Title == "New service request" &&
            n.Message.Contains(" requested ") &&
            !n.Message.Contains("Id:"));
    }

    [Fact(DisplayName =
        "P6-N2: a rejection notifies the client company and carries the reason")]
    public async Task Reject_NotifiesClientWithReason()
    {
        var serviceRequestId = await SeedServiceRequestAsync(null);

        await EnsureOk(
            await AdminRejectAsync(
                serviceRequestId, "The visa has not expired yet."));

        var clientNotifications = await NotificationsForAsync(ClientA);

        Assert.Contains(clientNotifications, n =>
            n.Title == "Service request rejected" &&
            n.Message.Contains("The visa has not expired yet."));

        // The other company's client is told nothing.
        var otherNotifications = await NotificationsForAsync(ClientB);

        Assert.DoesNotContain(otherNotifications, n =>
            n.Title == "Service request rejected");
    }

    [Fact(DisplayName =
        "P6-N3: a conversion notifies the client company and carries the new task id")]
    public async Task Convert_NotifiesClientWithTaskId()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        var response = await AdminConvertAsync(serviceRequestId);

        await EnsureOk(response, HttpStatusCode.OK);

        var body =
            await ReadJsonAsync<TestResponse<ConvertResponseDto>>(response);

        var clientNotifications = await NotificationsForAsync(ClientA);

        Assert.Contains(clientNotifications, n =>
            n.Title == "Service request converted" &&
            n.Message.Contains(body!.Data!.RenewalTaskId.ToString()));
    }

    [Fact(DisplayName =
        "P6-N4: a failed conversion notifies nobody")]
    public async Task FailedConvert_SendsNoNotification()
    {
        var serviceRequestId =
            await SeedServiceRequestAsync(documentId: null);

        var response = await AdminConvertAsync(serviceRequestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var clientNotifications = await NotificationsForAsync(ClientA);

        Assert.DoesNotContain(clientNotifications, n =>
            n.Title == "Service request converted");
    }

    // ==================================================================
    // Authorization
    // ==================================================================

    [Fact(DisplayName =
        "P6-S1: an agent cannot convert or reject a service request")]
    public async Task Decision_AgentIsForbidden()
    {
        var documentId =
            await SeedActiveVisaAsync(employeeId: TestDataSeeder.Ids.EmployeeA);

        var serviceRequestId = await SeedServiceRequestAsync(documentId);

        var convert = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{serviceRequestId}/convert",
                null,
                AgentA, role: "Agent"));

        Assert.Equal(HttpStatusCode.Forbidden, convert.StatusCode);

        var reject = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{serviceRequestId}/reject",
                new { reason = "Should not be allowed." },
                AgentA, role: "Agent"));

        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);

        // A refused call writes nothing, so the request is still decidable.
        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
        Assert.Equal(0, await TaskCountForDocumentAsync(documentId));
    }

    [Fact(DisplayName =
        "P6-S2: a client cannot decide their own request")]
    public async Task Decision_ClientIsForbidden()
    {
        var serviceRequestId = await SeedServiceRequestAsync(null);

        var reject = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{serviceRequestId}/reject",
                new { reason = "I reject my own request." },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);

        var request = await LoadRequestAsync(serviceRequestId);

        Assert.Equal(ServiceRequestStatus.Submitted, request!.Status);
    }
}
