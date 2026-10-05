using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Identity;

namespace PROManagementSystem.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds a deterministic, realistic mock client company ("FAMILFA TECH
/// SOLUTIONS L.L.C") plus the full hierarchy needed to exercise the client
/// billing / payment / renewal flow and the account-approval workflow end to
/// end against a local development database.
///
/// This is a distinct world from the existing FAMIL mock data
/// (MockClientCompanySeeder / faamil@gmail.com): the client here is
/// familfa@gmail.com, seeded as a fully active/approved/verified demo Client
/// (IsApproved=true, IsActive=true, EmailVerified=true) so every client-side
/// endpoint can be exercised immediately. Existing rows for the same email
/// are re-activated on each dev startup (self-healing), never duplicated.
///
/// The seeder is idempotent: every row is keyed by a stable GUID plus at
/// least one natural key (email / document number / invoice number /
/// contract number / government reference), and a row is only inserted when
/// neither exists. It never deletes or truncates, so it is safe to run
/// repeatedly. If a user for the mock client email already exists in the
/// database (e.g. a pre-seeder registration into the local database) that
/// row is re-associated with the mock company instead of being duplicated.
/// It deliberately skips PaymentOrders (the client creates those manually)
/// and never marks an invoice as Paid.
///
/// It is wired into the same development-only flow as
/// MockClientCompanySeeder: invoked from Program.cs, guarded by the
/// "Seeding:EnableMockClientData" flag which exists only in
/// appsettings.Local.json. Production never seeds.
/// </summary>
public static class MockFamilfaClientSeeder
{
    /// <summary>Deterministic sign-in password for the mock client (and the
    /// shared agent user this seeder may create).</summary>
    public const string LoginPassword = "TestPassword123!";

    public const string CompanyName = "FAMILFA TECH SOLUTIONS L.L.C";
    public const string EntityName = "FAMILFA TECH SOLUTIONS - Dubai Branch";

    public const string ClientEmail = "familfa@gmail.com";

    public const string ContractNumber = "CTR-FAMILFA-2026-001";
    public const string RetainerInvoiceNumber = "RET-FAMILFA-2026-001";
    public const string ServiceFeeInvoiceNumber = "SF-FAMILFA-2026-001";
    public const string GovernmentReference = "GOV-FAMILFA-2026-001";

    /// <summary>BCrypt hash of <see cref="LoginPassword"/>.</summary>
    private static readonly IPasswordHasher _passwordHasher =
        new BCryptPasswordHasher();

    private static readonly string PasswordHash =
        _passwordHasher.Hash(LoginPassword);

    /// <summary>
    /// Deterministic GUIDs for the mock "world G" dataset. Values use the
    /// same stable-format scheme as the test seeder and the existing mock
    /// world F so re-runs never collide with themselves or the A/B/F worlds.
    /// </summary>
    public static class Ids
    {
        // User ids (reuses the shared Agent A id so task assignment rows
        // always resolve; the agent guard below creates it if missing).
        public static readonly Guid Agent = Guid("000000000002");
        public static readonly Guid Client = Guid("000000002f01");

        // Company / entity hierarchy
        public static readonly Guid Company = Guid("000000002f02");
        public static readonly Guid Entity = Guid("000000002f03");

        // Employees
        public static readonly Guid EmployeeHassan = Guid("000000002f10");
        public static readonly Guid EmployeeFatima = Guid("000000002f11");

        // Entity-owned documents
        public static readonly Guid TradeLicense = Guid("000000002f20");
        public static readonly Guid EstablishmentCard = Guid("000000002f21");

        // Employee-owned documents
        public static readonly Guid HassanPassport = Guid("000000002f22");
        public static readonly Guid HassanVisa = Guid("000000002f23");
        public static readonly Guid HassanEmiratesId = Guid("000000002f24");
        public static readonly Guid HassanLaborCard = Guid("000000002f25");
        public static readonly Guid FatimaPassport = Guid("000000002f26");
        public static readonly Guid FatimaVisa = Guid("000000002f27");
        public static readonly Guid FatimaEmiratesId = Guid("000000002f28");
        public static readonly Guid FatimaLaborCard = Guid("000000002f29");

        // Service contract
        public static readonly Guid Contract = Guid("000000002f30");

        // Renewal tasks + histories
        public static readonly Guid TaskSubmitted = Guid("000000002f40");
        public static readonly Guid TaskAssigned = Guid("000000002f41");
        public static readonly Guid TaskCompleted = Guid("000000002f42");
        public static readonly Guid HistorySubmitted = Guid("000000002f43");
        public static readonly Guid HistoryAssigned = Guid("000000002f44");
        public static readonly Guid HistoryCompleted = Guid("000000002f45");

        // Service request
        public static readonly Guid ServiceRequest = Guid("000000002f46");

        // Notifications
        public static readonly Guid NotificationWelcome = Guid("000000002f47");
        public static readonly Guid NotificationExpiring = Guid("000000002f48");

        // Billing
        public static readonly Guid RetainerInvoice = Guid("000000002f50");
        public static readonly Guid ServiceFeeInvoice = Guid("000000002f51");
        public static readonly Guid GovFeeDisbursement = Guid("000000002f52");

        private static Guid Guid(string hex)
            => System.Guid.Parse(
                $"00000000-0000-0000-0000-{hex.PadLeft(12, '0')}");
    }

    /// <summary>
    /// Inserts the mock dataset if it is not already present.
    /// </summary>
    public static void Seed(
        ApplicationDbContext db,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        using var transaction = db.Database.BeginTransaction();

        var existingUserIds = db.Users.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingUserEmailIds = db.Users.IgnoreQueryFilters()
            .Select(x => new { x.Id, x.Email })
            .ToList()
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id,
                StringComparer.OrdinalIgnoreCase);
        var existingUserEmails = existingUserEmailIds.Keys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingCompanyIds = db.ClientCompanies.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingCompanyNames = db.ClientCompanies.IgnoreQueryFilters()
            .Select(x => x.CompanyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEntityIds = db.ClientEntities.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingEmployeeIds = db.Employees.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingDocumentIds = db.Documents.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingDocumentNumbers = db.Documents.IgnoreQueryFilters()
            .Select(x => x.DocumentNumber).ToHashSet();
        var existingContractIds = db.ServiceContracts.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingContractNumbers = db.ServiceContracts.IgnoreQueryFilters()
            .Select(x => x.ContractNumber).ToHashSet();
        var existingTaskIds = db.RenewalTasks.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingRetainerIds = db.RetainerInvoices.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingRetainerNumbers = db.RetainerInvoices.IgnoreQueryFilters()
            .Select(x => x.InvoiceNumber).ToHashSet();
        var existingServiceFeeIds = db.ServiceFeeInvoices.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingServiceFeeNumbers = db.ServiceFeeInvoices.IgnoreQueryFilters()
            .Select(x => x.InvoiceNumber).ToHashSet();
        var existingGovFeeIds = db.GovFeeDisbursements.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingHistoryIds = db.RenewalTaskHistories.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingServiceRequestIds = db.ServiceRequests.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();
        var existingNotificationIds = db.Notifications.IgnoreQueryFilters()
            .Select(x => x.Id).ToHashSet();

        // ------------------------------------------------------------------
        // Users
        // ------------------------------------------------------------------

        // The shared Agent A account (used by task assignment rows in this
        // world and the existing mock world F). Created only if it does not
        // already exist so re-runs stay duplicate-free.
        if (!existingUserIds.Contains(Ids.Agent) &&
            !existingUserEmails.Contains("agent-a@test.local"))
        {
            db.Users.Add(User(Ids.Agent, "Test Agent (E2E)",
                "agent-a@test.local", UserRole.Agent));
        }

        // The client account may already exist in a previously-used local
        // database (registered before this seeder existed) under an
        // auto-generated id and no company association. Re-associate that row
        // instead of duplicating it, and fully activate it so the account is
        // immediately usable as a demo client (Role=Client,
        // IsActive+IsApproved+EmailVerified=true, linked to the mock
        // company). Every startup re-runs this so the flags self-heal.
        if (existingUserEmailIds.TryGetValue(
                ClientEmail, out var existingClientUserId))
        {
            var existingClient = db.Users.IgnoreQueryFilters()
                .Single(x => x.Id == existingClientUserId);
            if (existingClient.Role != UserRole.Client ||
                !existingClient.IsActive ||
                !existingClient.IsApproved ||
                !existingClient.EmailVerified ||
                existingClient.ClientCompanyId != Ids.Company)
            {
                existingClient.Role = UserRole.Client;
                existingClient.IsActive = true;
                existingClient.IsApproved = true;
                existingClient.EmailVerified = true;
                existingClient.ClientCompanyId = Ids.Company;
                existingClient.UpdatedAt = DateTime.UtcNow;
            }

            // The demo credential must always work after a reuse. A row that
            // pre-dates the seeder keeps its own BCrypt hash, so the documented
            // LoginPassword silently fails. Reset it here so a reused account is
            // a true drop-in demo client (idempotent, self-healing).
            if (!_passwordHasher.Verify(
                    LoginPassword,
                    existingClient.PasswordHash))
            {
                existingClient.PasswordHash =
                    _passwordHasher.Hash(LoginPassword);
                existingClient.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (!existingUserIds.Contains(Ids.Client))
        {
            db.Users.Add(new User
            {
                Id = Ids.Client,
                FullName = "Faizal Rahman",
                Email = ClientEmail,
                PasswordHash = PasswordHash,
                Role = UserRole.Client,
                IsActive = true,
                IsApproved = true,
                EmailVerified = true,
                ClientCompanyId = Ids.Company,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        // The actual client user id resolved after the reuse/create branch
        // above. Notification rows must target this real id (a pre-existing
        // account keeps its own id; only a fresh account uses Ids.Client).
        var clientUserId = existingUserEmailIds.TryGetValue(
                ClientEmail, out var resolvedClientUserId)
            ? resolvedClientUserId
            : Ids.Client;

        // ------------------------------------------------------------------
        // Company / entity / employees
        // ------------------------------------------------------------------

        if (!existingCompanyIds.Contains(Ids.Company) &&
            !existingCompanyNames.Contains(CompanyName))
        {
            db.ClientCompanies.Add(new ClientCompany
            {
                Id = Ids.Company,
                CompanyName = CompanyName,
                TradeLicenseNumber = "TL-FAMILFA-2026-001",
                Phone = "+971500000002",
                Email = "company@familfa-test.local",
                Address = "Al Zahia, Sharjah, United Arab Emirates",
                Emirate = "Sharjah",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        if (!existingEntityIds.Contains(Ids.Entity))
        {
            db.ClientEntities.Add(new ClientEntity
            {
                Id = Ids.Entity,
                ClientCompanyId = Ids.Company,
                EntityName = EntityName,
                TradeLicenseNumber = "1122334",
                Emirate = "Sharjah",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        if (!existingEmployeeIds.Contains(Ids.EmployeeHassan))
        {
            db.Employees.Add(new Employee
            {
                Id = Ids.EmployeeHassan,
                ClientEntityId = Ids.Entity,
                FullName = "Hassan Ali",
                PassportNumber = "P-FAMILFA-0001",
                DateOfBirth = new DateTime(1991, 3, 12, 0, 0, 0,
                    DateTimeKind.Utc),
                Nationality = "Indian",
                JobTitle = "Software Engineer",
                HireDate = new DateTime(2024, 2, 1, 0, 0, 0,
                    DateTimeKind.Utc),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        if (!existingEmployeeIds.Contains(Ids.EmployeeFatima))
        {
            db.Employees.Add(new Employee
            {
                Id = Ids.EmployeeFatima,
                ClientEntityId = Ids.Entity,
                FullName = "Fatima Noor",
                PassportNumber = "P-FAMILFA-0002",
                DateOfBirth = new DateTime(1994, 9, 25, 0, 0, 0,
                    DateTimeKind.Utc),
                Nationality = "Pakistani",
                JobTitle = "PRO Coordinator",
                HireDate = new DateTime(2023, 7, 15, 0, 0, 0,
                    DateTimeKind.Utc),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        // ------------------------------------------------------------------
        // Documents
        // ------------------------------------------------------------------

        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.TradeLicense, "TEST-TL-001",
            DocumentType.TradeLicense,
            entityId: Ids.Entity,
            issueDate: now.AddMonths(-20),
            expiryDate: now.AddYears(4),
            fileName: "familfa-trade-license.pdf");

        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.EstablishmentCard, "TEST-EC-001",
            DocumentType.EstablishmentCard,
            entityId: Ids.Entity,
            issueDate: now.AddMonths(-20),
            expiryDate: now.AddYears(3),
            fileName: "familfa-establishment-card.pdf");

        // Hassan Ali - passport expires within 30 days.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.HassanPassport, "TEST-PASS-001",
            DocumentType.Passport,
            employeeId: Ids.EmployeeHassan,
            issueDate: now.AddYears(-4),
            expiryDate: now.AddDays(25),
            fileName: "familfa-hassan-ali-passport.pdf");

        // Hassan Ali - visa expires within 60 days.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.HassanVisa, "TEST-VISA-001",
            DocumentType.Visa,
            employeeId: Ids.EmployeeHassan,
            issueDate: now.AddMonths(-6),
            expiryDate: now.AddDays(55),
            fileName: "familfa-hassan-ali-visa.pdf");

        // Hassan Ali - Emirate ID with a comfortable expiry ahead.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.HassanEmiratesId, "TEST-EID-001",
            DocumentType.EmiratesId,
            employeeId: Ids.EmployeeHassan,
            issueDate: now.AddYears(-2),
            expiryDate: now.AddYears(1),
            fileName: "familfa-hassan-ali-emirates-id.pdf");

        // Hassan Ali - labor card expiring mid-window (drives the completed
        // renewal task so the daily expiry job ignores nothing new).
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.HassanLaborCard, "TEST-LABOR-001",
            DocumentType.LaborCard,
            employeeId: Ids.EmployeeHassan,
            issueDate: now.AddMonths(-3),
            expiryDate: now.AddDays(45),
            fileName: "familfa-hassan-ali-labor-card.pdf");

        // Fatima Noor - passport expires within 90 days.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.FatimaPassport, "TEST-PASS-002",
            DocumentType.Passport,
            employeeId: Ids.EmployeeFatima,
            issueDate: now.AddYears(-4),
            expiryDate: now.AddDays(85),
            fileName: "familfa-fatima-noor-passport.pdf");

        // Fatima Noor - visa with a comfortable expiry ahead.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.FatimaVisa, "TEST-VISA-002",
            DocumentType.Visa,
            employeeId: Ids.EmployeeFatima,
            issueDate: now.AddMonths(-4),
            expiryDate: now.AddYears(1).AddMonths(6),
            fileName: "familfa-fatima-noor-visa.pdf");

        // Fatima Noor - Emirate ID with a comfortable expiry ahead.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.FatimaEmiratesId, "TEST-EID-002",
            DocumentType.EmiratesId,
            employeeId: Ids.EmployeeFatima,
            issueDate: now.AddYears(-3),
            expiryDate: now.AddYears(2),
            fileName: "familfa-fatima-noor-emirates-id.pdf");

        // Fatima Noor - labor card with a comfortable expiry ahead.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.FatimaLaborCard, "TEST-LABOR-002",
            DocumentType.LaborCard,
            employeeId: Ids.EmployeeFatima,
            issueDate: now.AddMonths(-6),
            expiryDate: now.AddYears(1).AddMonths(2),
            fileName: "familfa-fatima-noor-labor-card.pdf");

        // ------------------------------------------------------------------
        // Service contract
        // ------------------------------------------------------------------

        if (!existingContractIds.Contains(Ids.Contract) &&
            !existingContractNumbers.Contains(ContractNumber))
        {
            db.ServiceContracts.Add(new ServiceContract
            {
                Id = Ids.Contract,
                ClientCompanyId = Ids.Company,
                ContractNumber = ContractNumber,
                StartDate = new DateTime(2026, 1, 1, 0, 0, 0,
                    DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 23, 59, 59,
                    DateTimeKind.Utc),
                Status = ServiceContractStatus.Active,
                RetainerAmount = 1000m,
                Terms =
                    "Mock annual retainer contract for Familfa client E2E testing.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        // ------------------------------------------------------------------
        // Renewal tasks + initial histories
        // ------------------------------------------------------------------

        if (!existingTaskIds.Contains(Ids.TaskSubmitted))
        {
            db.RenewalTasks.Add(RenewalTask(
                Ids.TaskSubmitted, Ids.HassanPassport, Ids.Company,
                assignedStaffId: null, RenewalTaskStatus.Submitted,
                serviceFeeAmount: null, completedAt: null, now));
        }

        if (!existingTaskIds.Contains(Ids.TaskAssigned))
        {
            db.RenewalTasks.Add(RenewalTask(
                Ids.TaskAssigned, Ids.HassanVisa, Ids.Company,
                assignedStaffId: Ids.Agent, RenewalTaskStatus.Submitted,
                serviceFeeAmount: null, completedAt: null, now));
        }

        if (!existingTaskIds.Contains(Ids.TaskCompleted))
        {
            db.RenewalTasks.Add(RenewalTask(
                Ids.TaskCompleted, Ids.HassanLaborCard, Ids.Company,
                assignedStaffId: Ids.Agent, RenewalTaskStatus.Updated,
                serviceFeeAmount: 750m, completedAt: now.AddDays(-30), now));
        }

        if (!existingHistoryIds.Contains(Ids.HistorySubmitted))
        {
            db.RenewalTaskHistories.Add(History(
                Ids.HistorySubmitted, Ids.TaskSubmitted,
                RenewalTaskStatus.Submitted, Ids.Agent, now,
                "Renewal task created (mock E2E)."));
        }

        if (!existingHistoryIds.Contains(Ids.HistoryAssigned))
        {
            db.RenewalTaskHistories.Add(History(
                Ids.HistoryAssigned, Ids.TaskAssigned,
                RenewalTaskStatus.Submitted, Ids.Agent, now,
                "Renewal task created and assigned to agent (mock E2E)."));
        }

        if (!existingHistoryIds.Contains(Ids.HistoryCompleted))
        {
            db.RenewalTaskHistories.Add(History(
                Ids.HistoryCompleted, Ids.TaskCompleted,
                RenewalTaskStatus.Updated, Ids.Agent, now.AddDays(-30),
                "Renewal task completed (mock E2E)."));
        }

        // ------------------------------------------------------------------
        // Service request
        // ------------------------------------------------------------------

        if (!existingServiceRequestIds.Contains(Ids.ServiceRequest))
        {
            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = Ids.ServiceRequest,
                ClientCompanyId = Ids.Company,
                Type = ServiceRequestType.EarlyRenewal,
                EmployeeId = Ids.EmployeeHassan,
                EntityId = Ids.Entity,
                DocumentId = Ids.HassanPassport,
                Status = ServiceRequestStatus.Submitted,
                Description =
                    "Early renewal service request for Hassan Ali (mock E2E).",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        // ------------------------------------------------------------------
        // Notifications (non-sensitive mock rows for the client inbox)
        // ------------------------------------------------------------------

        if (!existingNotificationIds.Contains(Ids.NotificationWelcome))
        {
            db.Notifications.Add(Notification(
                Ids.NotificationWelcome, clientUserId,
                "Welcome to the PRO platform",
                "Your mock client workspace is ready (test data).", now));
        }

        if (!existingNotificationIds.Contains(Ids.NotificationExpiring))
        {
            db.Notifications.Add(Notification(
                Ids.NotificationExpiring, clientUserId,
                "Document expiring soon",
                "Hassan Ali's passport expires within 30 days (mock data).",
                now));
        }

        // ------------------------------------------------------------------
        // Billing: pending invoices + gov-fee disbursement (no payment orders)
        // ------------------------------------------------------------------

        if (!existingRetainerIds.Contains(Ids.RetainerInvoice) &&
            !existingRetainerNumbers.Contains(RetainerInvoiceNumber))
        {
            db.RetainerInvoices.Add(new RetainerInvoice
            {
                Id = Ids.RetainerInvoice,
                ClientCompanyId = Ids.Company,
                ServiceContractId = Ids.Contract,
                InvoiceNumber = RetainerInvoiceNumber,
                InvoiceDate = now.AddDays(-20),
                DueDate = now.AddDays(10),
                PeriodStart = now.AddDays(-30),
                PeriodEnd = now.AddDays(1),
                Amount = 1000m,
                Currency = "AED",
                Status = RetainerInvoiceStatus.Pending,
                Notes = "Mock retainer invoice for manual end-to-end testing.",
                PaidAt = null,
                VoidedAt = null,
                VoidReason = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        if (!existingServiceFeeIds.Contains(Ids.ServiceFeeInvoice) &&
            !existingServiceFeeNumbers.Contains(ServiceFeeInvoiceNumber))
        {
            db.ServiceFeeInvoices.Add(new ServiceFeeInvoice
            {
                Id = Ids.ServiceFeeInvoice,
                ClientCompanyId = Ids.Company,
                RenewalTaskId = Ids.TaskCompleted,
                InvoiceNumber = ServiceFeeInvoiceNumber,
                InvoiceDate = now.AddDays(-10),
                DueDate = now.AddDays(10),
                Amount = 750m,
                Currency = "AED",
                Status = ServiceFeeInvoiceStatus.Pending,
                Description =
                    "Mock service fee invoice for completed labor-card renewal (E2E).",
                PaidAt = null,
                VoidedAt = null,
                VoidReason = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        if (!existingGovFeeIds.Contains(Ids.GovFeeDisbursement))
        {
            db.GovFeeDisbursements.Add(new GovFeeDisbursement
            {
                Id = Ids.GovFeeDisbursement,
                ClientCompanyId = Ids.Company,
                RenewalTaskId = Ids.TaskAssigned,
                FeeDescription = "Visa renewal government fee",
                GovernmentReference = GovernmentReference,
                Amount = 1250m,
                Currency = "AED",
                Status = GovFeeDisbursementStatus.InvoicedToClient,
                PaidByFirmAt = now.AddDays(-8),
                InvoicedAt = now.AddDays(-6),
                ReimbursedAt = null,
                Notes = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        db.SaveChanges();
        transaction.Commit();
    }

    private static void AddDocumentIfMissing(
        ApplicationDbContext db,
        HashSet<Guid> existingIds,
        HashSet<string> existingNumbers,
        Guid id,
        string documentNumber,
        DocumentType type,
        DateTime issueDate,
        DateTime expiryDate,
        string fileName,
        Guid? entityId = null,
        Guid? employeeId = null,
        long fileSize = 4096)
    {
        if (existingIds.Contains(id) || existingNumbers.Contains(documentNumber))
        {
            return;
        }

        db.Documents.Add(new Document
        {
            Id = id,
            ClientEntityId = entityId,
            EmployeeId = employeeId,
            Type = type,
            DocumentNumber = documentNumber,
            IssueDate = issueDate,
            ExpiryDate = expiryDate,
            FileUrl =
                $"https://storage.example.test/mock/{fileName}",
            FileName = fileName,
            ContentType = "application/pdf",
            FileSize = fileSize,
            Details = "{}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        });
    }

    private static User User(
        Guid id,
        string fullName,
        string email,
        UserRole role,
        Guid? clientCompanyId = null)
        => new()
        {
            Id = id,
            FullName = fullName,
            Email = email,
            PasswordHash = PasswordHash,
            Role = role,
            IsActive = true,
            IsApproved = true,
            EmailVerified = true,
            ClientCompanyId = clientCompanyId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static RenewalTask RenewalTask(
        Guid id,
        Guid documentId,
        Guid companyId,
        Guid? assignedStaffId,
        RenewalTaskStatus status,
        decimal? serviceFeeAmount,
        DateTime? completedAt,
        DateTime now)
        => new()
        {
            Id = id,
            DocumentId = documentId,
            ClientCompanyId = companyId,
            AssignedStaffId = assignedStaffId,
            Status = status,
            BlockedReason = null,
            BlockedSince = null,
            CompletedAt = completedAt,
            ServiceFeeAmount = serviceFeeAmount,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

    private static RenewalTaskHistory History(
        Guid id,
        Guid taskId,
        RenewalTaskStatus status,
        Guid changedBy,
        DateTime changedAt,
        string note)
        => new()
        {
            Id = id,
            RenewalTaskId = taskId,
            Status = status,
            ChangedBy = changedBy,
            ChangedAt = changedAt,
            Note = note,
            CreatedAt = changedAt,
            UpdatedAt = changedAt,
            IsDeleted = false,
            DeletedAt = null
        };

    private static Notification Notification(
        Guid id,
        Guid userId,
        string title,
        string message,
        DateTime now)
        => new()
        {
            Id = id,
            UserId = userId,
            Title = title,
            Message = message,
            IsRead = false,
            ReadAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
}