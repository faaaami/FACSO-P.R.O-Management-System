using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Persistence.Seeders;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.AuditLogging;

/// <summary>
/// Locks in the "Important" AuditLog coverage gap-closing increment:
/// authentication/security events, document lifecycle (admin + agent),
/// service-fee invoice creation (API + event driven), automatic renewal-task
/// creation, implicit contract termination and payment-order retry.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AuditLogCoverageTests : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;
    private static readonly Guid CompanyB = TestDataSeeder.Ids.CompanyB;
    private static readonly Guid EntityA = TestDataSeeder.Ids.EntityA;
    private static readonly Guid EmployeeA = TestDataSeeder.Ids.EmployeeA;
    private static readonly Guid DocumentA = TestDataSeeder.Ids.DocumentA;
    private static readonly Guid DocumentB = TestDataSeeder.Ids.DocumentB;
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;
    private static readonly Guid TaskB = TestDataSeeder.Ids.TaskB;
    private static readonly Guid ContractA = TestDataSeeder.Ids.ContractA;
    private static readonly Guid RetainerInvoiceA = TestDataSeeder.Ids.RetainerInvoiceA;
    private static readonly Guid ServiceFeeInvoiceA = TestDataSeeder.Ids.ServiceFeeInvoiceA;
    private static readonly Guid EmptyGuid = Guid.Empty;

    // Matches TestApplicationFactory: ["RenewalAutomation:UserId"].
    private static readonly Guid AutomationUserId =
        Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    public AuditLogCoverageTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private Task<HttpResponseMessage> PostAsAdminAsync(
        string url,
        object? body)
        => ScopeClient.SendAsync(
            ScopePost(url, body, Admin, "Admin"));

    private async Task<List<AuditLog>> FindAuditsAsync(
        string action,
        Guid? entityId = null)
    {
        List<AuditLog> result = null!;

        await WithScopeDbContextAsync(db =>
        {
            var query = db.AuditLogs
                .Where(x => x.Action == action);

            if (entityId.HasValue)
            {
                query = query
                    .Where(x => x.EntityId == entityId.Value);
            }

            result = query.ToList();
        });

        return result;
    }

    private static string HashToken(string token)
        => Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));

    private static Guid GuidOf(JsonNode node, string property)
        => Guid.Parse(node["data"]![property]!.GetValue<string>());

    private Task SeedUserAsync(
        Guid userId,
        string email,
        bool emailVerified = true)
        => WithScopeDbContextAsync(db =>
        {
            db.Users.Add(new User
            {
                Id = userId,
                FullName = "Audit Scoped User",
                Email = email,
                PasswordHash = "not-used",
                Role = UserRole.Agent,
                IsActive = true,
                IsApproved = true,
                EmailVerified = emailVerified,
                ClientCompanyId = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

    private Task SeedExtractionAsync(
        Guid extractionId,
        Guid? createdBy = null)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(
                new DocumentExtraction
                {
                    Id = extractionId,
                    FileName = "upload.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    SuggestedType = DocumentType.Passport,
                    SuggestedDocumentNumber = "DOC-EXTR-001",
                    SuggestedIssueDate = null,
                    SuggestedExpiryDate = null,
                    SuggestedOwnerType = "Employee",
                    Confidence = 0.95m,
                    Status = "Pending",
                    ExpiresAt = now.AddHours(24),
                    CreatedBy = createdBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

    private Task SeedUpdatedTaskAsync(Guid taskId)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = taskId,
                DocumentId = DocumentA,
                ClientCompanyId = CompanyA,
                AssignedStaffId = AgentA,
                Status = RenewalTaskStatus.Updated,
                ServiceFeeAmount = 150m,
                CompletedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

    // ==================================================================
    // Part 1 - Authentication / security events
    // ==================================================================

    [Fact(DisplayName =
        "Successful login writes a LoginSucceeded audit attributed to the user")]
    public async Task Login_Success_WritesLoginSucceededAudit()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = "agent-a@test.local",
                password = TestDataSeeder.TestLoginPassword
            });

        await EnsureOk(response, HttpStatusCode.OK);

        var audits = await FindAuditsAsync(
            "LoginSucceeded", AgentA);

        Assert.Single(audits);
        Assert.Equal(AgentA, audits[0].UserId);
    }

    [Fact(DisplayName =
        "Wrong password returns 401 and writes a LoginFailed audit against the user")]
    public async Task Login_WrongPassword_WritesLoginFailedAudit()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = "agent-a@test.local",
                password = "WrongPassword!"
            });

        await EnsureOk(response, HttpStatusCode.Unauthorized);

        var audits = await FindAuditsAsync(
            "LoginFailed", AgentA);

        Assert.Single(audits);
        Assert.Null(audits[0].UserId);
    }

    [Fact(DisplayName =
        "Unknown email returns 401 and writes a LoginFailed audit with an empty entity id")]
    public async Task Login_UnknownEmail_WritesLoginFailedAudit()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = "nobody@test.local",
                password = TestDataSeeder.TestLoginPassword
            });

        await EnsureOk(response, HttpStatusCode.Unauthorized);

        var audits = await FindAuditsAsync(
            "LoginFailed", EmptyGuid);

        Assert.Single(audits);
        Assert.Null(audits[0].UserId);
    }

    [Fact(DisplayName =
        "Auth audit descriptions never contain the raw password")]
    public async Task AuthAudits_DoNotExposeThePassword()
    {
        const string password = TestDataSeeder.TestLoginPassword;

        await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "agent-a@test.local", password });

        var audits = await FindAuditsAsync("LoginSucceeded");

        foreach (var audit in audits)
        {
            Assert.DoesNotContain(password, audit.Description);
            Assert.Null(audit.Metadata);
        }
    }

    [Fact(DisplayName =
        "Refresh token rotation writes a RefreshTokenIssued audit")]
    public async Task Refresh_WritesRefreshTokenIssuedAudit()
    {
        var login = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = "agent-a@test.local",
                password = TestDataSeeder.TestLoginPassword
            });

        await EnsureOk(login, HttpStatusCode.OK);

        var loginNode = JsonNode.Parse(
            await login.Content.ReadAsStringAsync())!;

        var refreshToken = loginNode["data"]!["refreshToken"]!
            .GetValue<string>();

        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken });

        await EnsureOk(response, HttpStatusCode.OK);

        var audits = await FindAuditsAsync(
            "RefreshTokenIssued", AgentA);

        Assert.Single(audits);
        Assert.Equal(AgentA, audits[0].UserId);
    }

    [Fact(DisplayName =
        "Logout writes a Logout audit for the revoked session")]
    public async Task Logout_WritesLogoutAudit()
    {
        var login = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = "agent-a@test.local",
                password = TestDataSeeder.TestLoginPassword
            });

        var loginNode = JsonNode.Parse(
            await login.Content.ReadAsStringAsync())!;

        var refreshToken = loginNode["data"]!["refreshToken"]!
            .GetValue<string>();

        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken });

        await EnsureOk(response, HttpStatusCode.OK);

        var audits = await FindAuditsAsync("Logout", AgentA);

        Assert.Single(audits);
        Assert.Equal(AgentA, audits[0].UserId);
    }

    [Fact(DisplayName =
        "Email verification writes an EmailVerified audit with no actor")]
    public async Task VerifyEmail_WritesEmailVerifiedAudit()
    {
        var userId = Guid.NewGuid();
        var rawToken = $"verify-{Guid.NewGuid():N}";

        await SeedUserAsync(
            userId, $"verify-{Guid.NewGuid():N}@test.local",
            emailVerified: false);

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.EmailVerificationTokens.Add(
                new EmailVerificationToken
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TokenHash = HashToken(rawToken),
                    ExpiresAt = now.AddHours(24),
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new { token = rawToken });

        await EnsureOk(response, HttpStatusCode.OK);

        var audits = await FindAuditsAsync(
            "EmailVerified", userId);

        Assert.Single(audits);
        Assert.Null(audits[0].UserId);
    }

    [Fact(DisplayName =
        "Password reset writes a PasswordReset audit with no actor")]
    public async Task ResetPassword_WritesPasswordResetAudit()
    {
        var rawToken = $"reset-{Guid.NewGuid():N}";

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.PasswordResetTokens.Add(
                new PasswordResetToken
                {
                    Id = Guid.NewGuid(),
                    UserId = AgentA,
                    TokenHash = HashToken(rawToken),
                    ExpiresAt = now.AddHours(24),
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            db.SaveChanges();
        });

        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new
            {
                token = rawToken,
                newPassword = TestDataSeeder.TestLoginPassword
            });

        await EnsureOk(response, HttpStatusCode.OK);

        var audits = await FindAuditsAsync(
            "PasswordReset", AgentA);

        Assert.Single(audits);
        Assert.Null(audits[0].UserId);
    }

    // ==================================================================
    // Part 2 - Document lifecycle (admin + agent)
    // ==================================================================

    [Fact(DisplayName =
        "Admin document update writes a DocumentUpdated audit attributed to the admin")]
    public async Task Admin_UpdateDocument_WritesDocumentUpdatedAudit()
    {
        var response = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/admin/documents/{DocumentA}",
                new
                {
                    type = "Passport",
                    documentNumber = "DOC-A-001-UPD",
                    issueDate = "2024-01-01T00:00:00Z",
                    expiryDate = "2030-01-01T00:00:00Z",
                    fileUrl = "https://storage.test/dummy",
                    fileName = "doc-updated.pdf",
                    contentType = "application/pdf",
                    fileSize = 4096,
                    isActive = true
                },
                Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var audits = await FindAuditsAsync(
            "DocumentUpdated", DocumentA);

        Assert.Single(audits);
        Assert.Equal(Admin, audits[0].UserId);
    }

    [Fact(DisplayName =
        "Agent document update writes a DocumentUpdated audit attributed to the agent")]
    public async Task Agent_UpdateDocument_WritesDocumentUpdatedAudit()
    {
        var response = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/agent/documents/{DocumentA}",
                new { documentNumber = "DOC-A-001-AGENT" },
                AgentA,
                "Agent"));

        await EnsureOk(response, HttpStatusCode.OK);

        var audits = await FindAuditsAsync(
            "DocumentUpdated", DocumentA);

        Assert.Single(audits);
        Assert.Equal(AgentA, audits[0].UserId);
    }

    [Fact(DisplayName =
        "Admin document extraction writes a DocumentExtractionStarted audit")]
    public async Task Admin_ExtractDocument_WritesExtractionStartedAudit()
    {
        using var form = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(
            new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2d });

        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/pdf");

        form.Add(fileContent, "file", "sample.pdf");

        var response = await ScopeClient.SendAsync(
            ApplyScope(
                new HttpRequestMessage(HttpMethod.Post,
                    "/api/v1/admin/documents/extract")
                {
                    Content = form
                },
                Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var node = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var extractionId = GuidOf(node, "extractionId");

        var audits = await FindAuditsAsync(
            "DocumentExtractionStarted", extractionId);

        Assert.Single(audits);
        Assert.Equal(Admin, audits[0].UserId);
    }

    [Fact(DisplayName =
        "Admin extraction confirmation writes confirmed + created audits")]
    public async Task Admin_ConfirmExtraction_WritesConfirmedAndCreatedAudits()
    {
        var extractionId = Guid.NewGuid();

        await SeedExtractionAsync(extractionId);

        var response = await ScopeClient.SendAsync(
            ApplyScope(
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/v1/admin/documents/extractions/{extractionId}/confirm")
                {
                    Content = JsonContent.Create(new
                    {
                        employeeId = EmployeeA,
                        clientEntityId = (Guid?)null,
                        type = "Passport",
                        documentNumber = "DOC-NEW-001",
                        details = new { passportNumber = "P-NEW-001" },
                        issueDate = "2023-01-01T00:00:00Z",
                        expiryDate = "2032-01-01T00:00:00Z",
                        fileUrl = "https://storage.test/dummy",
                        fileName = "confirmed.pdf",
                        contentType = "application/pdf",
                        fileSize = 2048
                    })
                },
                Admin,
                "Admin"));

        await EnsureOk(response, System.Net.HttpStatusCode.Created);

        var confirmed = await FindAuditsAsync(
            "DocumentExtractionConfirmed", extractionId);

        Assert.Single(confirmed);
        Assert.Equal(Admin, confirmed[0].UserId);

        var node = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var documentId = GuidOf(node, "documentId");

        var created = await FindAuditsAsync(
            "DocumentCreated", documentId);

        Assert.Single(created);
        Assert.Equal(Admin, created[0].UserId);
    }

    [Fact(DisplayName =
        "Agent extraction confirmation writes confirmed + created audits")]
    public async Task Agent_ConfirmExtraction_WritesConfirmedAndCreatedAudits()
    {
        var extractionId = Guid.NewGuid();

        await SeedExtractionAsync(extractionId, createdBy: AgentA);

        var response = await ScopeClient.SendAsync(
            ApplyScope(
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/v1/agent/documents/extractions/{extractionId}/confirm")
                {
                    Content = JsonContent.Create(new
                    {
                        employeeId = EmployeeA,
                        clientEntityId = (Guid?)null,
                        type = "Passport",
                        documentNumber = "DOC-NEW-A-001",
                        details = new { passportNumber = "P-NEW-A-001" },
                        issueDate = "2023-01-01T00:00:00Z",
                        expiryDate = "2032-01-01T00:00:00Z",
                        fileUrl = "https://storage.test/dummy",
                        fileName = "confirmed.pdf",
                        contentType = "application/pdf",
                        fileSize = 2048
                    })
                },
                AgentA,
                "Agent"));

        await EnsureOk(response, System.Net.HttpStatusCode.Created);

        var confirmed = await FindAuditsAsync(
            "DocumentExtractionConfirmed", extractionId);

        Assert.Single(confirmed);
        Assert.Equal(AgentA, confirmed[0].UserId);

        var node = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var documentId = GuidOf(node, "documentId");

        var created = await FindAuditsAsync(
            "DocumentCreated", documentId);

        Assert.Single(created);
        Assert.Equal(AgentA, created[0].UserId);
    }

    [Fact(DisplayName =
        "Creating and deleting a document dependency writes both audits")]
    public async Task Admin_CreateAndDeleteDependency_WritesAudits()
    {
        var createResponse = await PostAsAdminAsync(
            $"/api/v1/admin/documents/{DocumentA}/dependencies",
            new { dependsOnDocumentId = DocumentB });

        await EnsureOk(createResponse, System.Net.HttpStatusCode.Created);

        var node = JsonNode.Parse(
            await createResponse.Content.ReadAsStringAsync())!;

        var dependencyId = GuidOf(node, "id");

        var created = await FindAuditsAsync(
            "DocumentDependencyCreated", dependencyId);

        Assert.Single(created);
        Assert.Equal(Admin, created[0].UserId);

        var deleteResponse = await ScopeClient.SendAsync(
            ApplyScope(
                new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"/api/v1/admin/documents/{DocumentA}/dependencies/{dependencyId}"),
                Admin,
                "Admin"));

        await EnsureOk(deleteResponse, HttpStatusCode.OK);

        var deleted = await FindAuditsAsync(
            "DocumentDependencyDeleted", dependencyId);

        Assert.Single(deleted);
        Assert.Equal(Admin, deleted[0].UserId);
    }

    // ==================================================================
    // Part 3 - Service fee invoice creation (API + event driven)
    // ==================================================================

    [Fact(DisplayName =
        "API service fee invoice creation writes a ServiceFeeInvoiceCreated audit")]
    public async Task Api_CreateServiceFeeInvoice_WritesServiceFeeInvoiceCreatedAudit()
    {
        var taskId = Guid.NewGuid();

        await SeedUpdatedTaskAsync(taskId);

        var response = await PostAsAdminAsync(
            "/api/v1/service-fee-invoices",
            new
            {
                clientCompanyId = CompanyA,
                renewalTaskId = taskId,
                invoiceNumber = $"INV-SF-TEST-{Guid.NewGuid():N}",
                invoiceDate = "2026-09-01",
                dueDate = "2026-10-01",
                amount = 150m,
                currency = "AED",
                description = "Test invoice."
            });

        await EnsureOk(response, System.Net.HttpStatusCode.Created);

        var node = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var invoiceId = GuidOf(node, "id");

        var audits = await FindAuditsAsync(
            "ServiceFeeInvoiceCreated", invoiceId);

        Assert.Single(audits);
        Assert.Equal(Admin, audits[0].UserId);
    }

    [Fact(DisplayName =
        "Renewal-task-completed event path also writes a ServiceFeeInvoiceCreated audit")]
    public async Task Event_ServiceFeeInvoiceCreation_WritesServiceFeeInvoiceCreatedAudit()
    {
        await WithScopeDbContextAsync(db =>
        {
            var taskB = db.RenewalTasks.First(x => x.Id == TaskB);

            taskB.Status = RenewalTaskStatus.Updated;
            taskB.CompletedAt = DateTime.UtcNow;
            taskB.ServiceFeeAmount = 150m;

            db.SaveChanges();
        });

        await using var scope = Factory.Services.CreateAsyncScope();

        var publisher = scope.ServiceProvider
            .GetRequiredService<IPublisher>();

        await publisher.Publish(
            new RenewalTaskCompletedEvent(TaskB, CompanyB));

        Guid? invoiceId = null;

        await WithScopeDbContextAsync(db =>
        {
            invoiceId = db.ServiceFeeInvoices
                .First(x => x.RenewalTaskId == TaskB)
                .Id;
        });

        var audits = await FindAuditsAsync(
            "ServiceFeeInvoiceCreated", invoiceId!.Value);

        Assert.Single(audits);
        Assert.Null(audits[0].UserId);
    }

    // ==================================================================
    // Part 4 - Automatic renewal task creation
    // ==================================================================

    [Fact(DisplayName =
        "RenewalAutomation:UserId resolves to the seeded automation system user")]
    public async Task RenewalAutomation_ConfiguredActor_IsTheSeededSystemUser()
    {
        using var scope = Factory.Services.CreateScope();

        var configured = scope.ServiceProvider
            .GetRequiredService<IConfiguration>()
            ["RenewalAutomation:UserId"];

        var configuredAutomationUserId =
            Guid.Parse(Assert.IsType<string>(configured));

        // The runtime configuration must point at the canonical automation
        // identity that is seeded (and which the test factory overrides to
        // the same value).
        Assert.Equal(AutomationUserId, configuredAutomationUserId);
        Assert.Equal(
            MockClientCompanySeeder.Ids.AutomationAdmin,
            configuredAutomationUserId);

        // The idempotent system-user seeder must materialize exactly that
        // actor (and stay idempotent on a second pass).
        await WithScopeDbContextAsync(db =>
        {
            AutomationUserSeeder.Seed(db, configuredAutomationUserId);
            AutomationUserSeeder.Seed(db, configuredAutomationUserId);
        });

        bool actorExists = false;

        await WithScopeDbContextAsync(db =>
        {
            actorExists = db.Users.Any(x =>
                x.Id == configuredAutomationUserId && !x.IsDeleted);
        });

        Assert.True(actorExists,
            "The configured automation actor must resolve to a real, " +
            "non-deleted user.");
    }

    [Fact(DisplayName =
        "Document expiry job writes a RenewalTaskCreated audit with the automation actor")]
    public async Task Job_CreateRenewalTask_WritesRenewalTaskCreatedAudit()
    {
        var newDocumentId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
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

            db.Documents.Add(new Document
            {
                Id = newDocumentId,
                EmployeeId = EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.EmiratesId,
                DocumentNumber = "DOC-EXP-001",
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(30),
                FileUrl = "https://storage.test/dummy",
                FileName = "expiring.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null,
                // Attach entity navigation for the company resolution.
                Employee = db.Employees.First(x => x.Id == EmployeeA)
            });

            db.SaveChanges();
        });

        await using var scope = Factory.Services.CreateAsyncScope();

        var job = scope.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();

        await job.ExecuteAsync();

        var audits = await FindAuditsAsync(
            "RenewalTaskCreated");

        var audit = Assert.Single(audits,
            x => x.Description!.Contains(
                newDocumentId.ToString()))!;

        Assert.Equal(AutomationUserId, audit.UserId);

        // The automation actor recorded on the audit must reference a real,
        // non-deleted user (the renewal_task_histories.ChangedBy FK and the
        // service guard require it), proving the actor is valid.
        bool actorExists = false;

        await WithScopeDbContextAsync(db =>
        {
            actorExists = db.Users.Any(x =>
                x.Id == audit.UserId && !x.IsDeleted);
        });

        Assert.True(actorExists,
            "The RenewalTaskCreated audit actor must be a real user.");
    }

    // ==================================================================
    // Part 5 - Implicit contract termination
    // ==================================================================

    [Fact(DisplayName =
        "Creating a new active contract ends the previous contract with a ServiceContractEnded audit")]
    public async Task CreateServiceContract_EndsActiveContract_WritesServiceContractEndedAudit()
    {
        var response = await PostAsAdminAsync(
            $"/api/v1/clients/{CompanyA}/contracts",
            new
            {
                contractNumber = "CT-A-002",
                startDate = "2026-09-01",
                retainerAmount = 1000m,
                terms = "Renewed terms."
            });

        await EnsureOk(response, HttpStatusCode.OK);

        var ended = await FindAuditsAsync(
            "ServiceContractEnded", ContractA);

        Assert.Single(ended);
        Assert.Equal(Admin, ended[0].UserId);

        var node = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var newContractId = GuidOf(node, "id");

        var created = await FindAuditsAsync(
            "ServiceContractCreated", newContractId);

        Assert.Single(created);
    }

    // ==================================================================
    // Part 6 - Payment order retry
    // ==================================================================

    [Fact(DisplayName =
        "Retrying a payment order writes a PaymentOrderRetried audit (no PaymentOrderCreated)")]
    public async Task PaymentOrderRetry_WritesPaymentOrderRetriedAudit()
    {
        var existingOrderId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = existingOrderId,
                InvoiceType = PaymentInvoiceType.RetainerInvoice,
                RetainerInvoiceId = RetainerInvoiceA,
                ServiceFeeInvoiceId = null,
                ClientCompanyId = CompanyA,
                Amount = 2500m,
                Currency = "AED",
                Status = PaymentStatus.Created,
                RazorpayOrderId = null,
                RazorpayPaymentId = null,
                RazorpaySignature = null,
                Notes = "Previous attempt failed before Razorpay order was created.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var response = await PostAsAdminAsync(
            "/api/v1/payment-orders",
            new
            {
                invoiceType = "RetainerInvoice",
                retainerInvoiceId = RetainerInvoiceA,
                serviceFeeInvoiceId = (Guid?)null,
                notes = (string?)null
            });

        await EnsureOk(response, System.Net.HttpStatusCode.Created);

        var retried = await FindAuditsAsync(
            "PaymentOrderRetried", existingOrderId);

        Assert.Single(retried);
        Assert.Equal(Admin, retried[0].UserId);
        Assert.Empty(await FindAuditsAsync("PaymentOrderCreated"));
    }

    [Fact(DisplayName =
        "A fresh payment order writes only PaymentOrderCreated, never PaymentOrderRetried")]
    public async Task FreshPaymentOrder_WritesOnlyPaymentOrderCreatedAudit()
    {
        var response = await PostAsAdminAsync(
            "/api/v1/payment-orders",
            new
            {
                invoiceType = "ServiceFeeInvoice",
                retainerInvoiceId = (Guid?)null,
                serviceFeeInvoiceId = ServiceFeeInvoiceA,
                notes = (string?)null
            });

        await EnsureOk(response, System.Net.HttpStatusCode.Created);

        var node = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var orderId = GuidOf(node, "id");

        Assert.Single(await FindAuditsAsync(
            "PaymentOrderCreated", orderId));
        Assert.Empty(await FindAuditsAsync(
            "PaymentOrderRetried", orderId));
    }

    private static HttpRequestMessage ApplyScope(
        HttpRequestMessage request,
        Guid userId,
        string role,
        string? clientCompanyId = null)
    {
        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                userId, role, clientCompanyId));
        return request;
    }
}