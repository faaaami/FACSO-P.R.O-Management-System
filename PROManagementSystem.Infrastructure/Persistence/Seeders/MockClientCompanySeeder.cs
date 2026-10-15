using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Identity;

namespace PROManagementSystem.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds a deterministic, realistic mock client company ("FAMIL TECHNOLOGY
/// SOLUTIONS L.L.C") plus the full hierarchy needed to exercise the client
/// billing / payment / renewal flow end to end against a local development
/// database.
///
/// The seeder is idempotent: every row is keyed by a stable GUID plus at
/// least one natural key (email / document number / invoice number /
/// contract number), and a row is only inserted when neither exists. It never
/// deletes or truncates, so it is safe to run repeatedly. If a user for the
/// mock client email already exists in the database (e.g. a pre-seeder
/// registration into the local database) that row is re-associated with the
/// mock company instead of being duplicated. It deliberately skips
/// PaymentOrders (the client creates those manually) and never marks an
/// invoice as Paid.
///
/// It is wired into two flows:
///   1. Automated tests - appended to <see cref="PROManagementSystem.Tests.Infrastructure.TestDataSeeder"/>.
///   2. Local development - invoked from Program.cs, guarded by the
///      "Seeding:EnableMockClientData" flag which exists only in
///      appsettings.Local.json. Production never seeds.
/// </summary>
public static class MockClientCompanySeeder
{
    /// <summary>Deterministic sign-in password for the mock client (and the
    /// agent/automation users this seeder creates).</summary>
    public const string LoginPassword = "TestPassword123!";

    public const string CompanyName = "FAMIL TECHNOLOGY SOLUTIONS L.L.C";
    public const string EntityName = "FAMIL TECHNOLOGY SOLUTIONS - Dubai Branch";

    public const string ClientEmail = "faamil@gmail.com";
    public const string AgentEmail = "agent-a@test.local";
    public const string AutomationEmail = "automation@internal.local";

    public const string ContractNumber = "CTR-FAMIL-2026-001";
    public const string RetainerInvoiceNumber = "RET-FAMIL-2026-001";
    public const string ServiceFeeInvoiceNumber = "SF-FAMIL-2026-001";
    public const string GovernmentReference = "GOV-FAMIL-2026-001";

    /// <summary>BCrypt hash of <see cref="LoginPassword"/>.</summary>
    private static readonly string PasswordHash =
        new BCryptPasswordHasher().Hash(LoginPassword);

    /// <summary>
    /// Deterministic GUIDs for the mock "world F" dataset. Values use the same
    /// stable-format scheme as the test seeder so re-runs never collide with
    /// themselves or the A/B test worlds.
    /// </summary>
    public static class Ids
    {
        // User ids (matches the existing Agent A id so test seeds reuse it).
        public static readonly Guid Agent = Guid("000000000002");
        public static readonly Guid AutomationAdmin = Guid("0000000000aa");
        public static readonly Guid Client = Guid("0000000001f1");

        // Company / entity hierarchy
        public static readonly Guid Company = Guid("000000000f01");
        public static readonly Guid Entity = Guid("000000000f02");
        public static readonly Guid EmployeeAhmed = Guid("000000000f10");
        public static readonly Guid EmployeeOmar = Guid("000000000f11");

        // Entity-owned documents
        public static readonly Guid TradeLicense = Guid("000000000f20");
        public static readonly Guid EstablishmentCard = Guid("000000000f21");

        // Employee-owned documents
        public static readonly Guid AhmedPassport = Guid("000000000f22");
        public static readonly Guid AhmedVisa = Guid("000000000f23");
        public static readonly Guid AhmedEmiratesId = Guid("000000000f24");
        public static readonly Guid OmarPassport = Guid("000000000f25");
        public static readonly Guid OmarVisa = Guid("000000000f26");
        public static readonly Guid OmarEmiratesId = Guid("000000000f27");

        // Service contract
        public static readonly Guid Contract = Guid("000000000f30");

        // Renewal tasks + histories
        public static readonly Guid TaskSubmitted = Guid("000000000f40");
        public static readonly Guid TaskAssigned = Guid("000000000f41");
        public static readonly Guid TaskCompleted = Guid("000000000f42");
        public static readonly Guid HistorySubmitted = Guid("000000000f43");
        public static readonly Guid HistoryAssigned = Guid("000000000f44");
        public static readonly Guid HistoryCompleted = Guid("000000000f45");

        // Billing
        public static readonly Guid RetainerInvoice = Guid("000000000f50");
        public static readonly Guid ServiceFeeInvoice = Guid("000000000f51");
        public static readonly Guid GovFeeDisbursement = Guid("000000000f52");

        private static Guid Guid(string hex)
            => System.Guid.Parse(
                $"00000000-0000-0000-0000-{hex.PadLeft(12, '0')}");
    }

    /// <summary>
    /// Inserts the mock dataset if it is not already present. Passing
    /// <paramref name="automationUserId"/> also creates the renewal-automation
    /// system user (used by the local Hangfire daily job); tests pass null so
    /// the A/B worlds remain the only seeded users there.
    /// </summary>
    public static void Seed(
        ApplicationDbContext db,
        Guid? automationUserId = null,
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

        // ------------------------------------------------------------------
        // Users
        // ------------------------------------------------------------------

        if (!existingUserIds.Contains(Ids.Agent) &&
            !existingUserEmails.Contains(AgentEmail))
        {
            db.Users.Add(User(Ids.Agent, "Test Agent (E2E)", AgentEmail,
                UserRole.Agent));
        }

        if (automationUserId.HasValue &&
            !existingUserIds.Contains(automationUserId.Value) &&
            !existingUserEmails.Contains(AutomationEmail))
        {
            db.Users.Add(User(automationUserId.Value,
                "Renewal Automation", AutomationEmail, UserRole.Admin));
        }

        // The client account may already exist in a previously-used local
        // database (registered before this seeder existed) under an
        // auto-generated id and no company association. Re-associate that row
        // instead of duplicating it.
        if (existingUserEmailIds.TryGetValue(
                ClientEmail, out var existingClientUserId))
        {
            var existingClient = db.Users.IgnoreQueryFilters()
                .Single(x => x.Id == existingClientUserId);
            if (existingClient.ClientCompanyId != Ids.Company)
            {
                existingClient.ClientCompanyId = Ids.Company;
                existingClient.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (!existingUserIds.Contains(Ids.Client))
        {
            db.Users.Add(User(Ids.Client, "FAMIL Technology Admin",
                ClientEmail, UserRole.Client, clientCompanyId: Ids.Company));
        }

        // ------------------------------------------------------------------
        // Company / entity / employees
        // ------------------------------------------------------------------

        if (!existingCompanyIds.Contains(Ids.Company))
        {
            db.ClientCompanies.Add(new ClientCompany
            {
                Id = Ids.Company,
                CompanyName = CompanyName,
                TradeLicenseNumber = "TL-FAMIL-2026-001",
                Phone = "+971500000001",
                Email = "company@famil-test.local",
                Address = "Business Bay, Dubai, United Arab Emirates",
                Emirate = "Dubai",
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
                TradeLicenseNumber = "1234567",
                Emirate = "Dubai",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        if (!existingEmployeeIds.Contains(Ids.EmployeeAhmed))
        {
            db.Employees.Add(new Employee
            {
                Id = Ids.EmployeeAhmed,
                ClientEntityId = Ids.Entity,
                FullName = "Ahmed Rahman",
                PassportNumber = "P-FAMIL-0001",
                DateOfBirth = new DateTime(1995, 4, 15, 0, 0, 0,
                    DateTimeKind.Utc),
                Nationality = "Indian",
                JobTitle = "Software Engineer",
                HireDate = new DateTime(2024, 1, 15, 0, 0, 0,
                    DateTimeKind.Utc),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });
        }

        if (!existingEmployeeIds.Contains(Ids.EmployeeOmar))
        {
            db.Employees.Add(new Employee
            {
                Id = Ids.EmployeeOmar,
                ClientEntityId = Ids.Entity,
                FullName = "Omar Khan",
                PassportNumber = "P-FAMIL-0002",
                DateOfBirth = new DateTime(1992, 8, 20, 0, 0, 0,
                    DateTimeKind.Utc),
                Nationality = "Pakistani",
                JobTitle = "PRO Coordinator",
                HireDate = new DateTime(2023, 6, 1, 0, 0, 0,
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
            Ids.TradeLicense, "TR-FAMIL-2026-001",
            DocumentType.TradeLicense,
            entityId: Ids.Entity,
            issueDate: now.AddMonths(-20),
            expiryDate: now.AddYears(4),
            fileName: "famil-trade-license.pdf");

        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.EstablishmentCard, "EC-FAMIL-2026-001",
            DocumentType.EstablishmentCard,
            entityId: Ids.Entity,
            issueDate: now.AddMonths(-20),
            expiryDate: now.AddYears(3),
            fileName: "famil-establishment-card.pdf");

        // Ahmed Rahman - passport expires within 30 days.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.AhmedPassport, "P-FAMIL-0001",
            DocumentType.Passport,
            employeeId: Ids.EmployeeAhmed,
            issueDate: now.AddYears(-4),
            expiryDate: now.AddDays(25),
            fileName: "famil-ahmed-rahman-passport.pdf");

        // Ahmed Rahman - visa expires within 60 days.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.AhmedVisa, "V-FAMIL-0001",
            DocumentType.Visa,
            employeeId: Ids.EmployeeAhmed,
            issueDate: now.AddMonths(-6),
            expiryDate: now.AddDays(55),
            fileName: "famil-ahmed-rahman-visa.pdf");

        // Ahmed Rahman - Emirates ID with a comfortable expiry ahead.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.AhmedEmiratesId, "EI-FAMIL-0001",
            DocumentType.EmiratesId,
            employeeId: Ids.EmployeeAhmed,
            issueDate: now.AddYears(-2),
            expiryDate: now.AddYears(1),
            fileName: "famil-ahmed-rahman-emirates-id.pdf");

        // Omar Khan - passport expires within 90 days.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.OmarPassport, "P-FAMIL-0002",
            DocumentType.Passport,
            employeeId: Ids.EmployeeOmar,
            issueDate: now.AddYears(-4),
            expiryDate: now.AddDays(85),
            fileName: "famil-omar-khan-passport.pdf");

        // Omar Khan - visa with a comfortable expiry ahead.
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.OmarVisa, "V-FAMIL-0002",
            DocumentType.Visa,
            employeeId: Ids.EmployeeOmar,
            issueDate: now.AddMonths(-4),
            expiryDate: now.AddYears(1).AddMonths(6),
            fileName: "famil-omar-khan-visa.pdf");

        // Omar Khan - Emirates ID with a comfortable expiry ahead (used by the
        // completed renewal task so the daily expiry job ignores it).
        AddDocumentIfMissing(db, existingDocumentIds, existingDocumentNumbers,
            Ids.OmarEmiratesId, "EI-FAMIL-0002",
            DocumentType.EmiratesId,
            employeeId: Ids.EmployeeOmar,
            issueDate: now.AddYears(-3),
            expiryDate: now.AddYears(2),
            fileName: "famil-omar-khan-emirates-id.pdf");

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
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                Status = ServiceContractStatus.Active,
                RetainerAmount = 1000m,
                Terms =
                    "Mock annual retainer contract for Fasco PRO integration testing.",
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
                Ids.TaskSubmitted, Ids.AhmedPassport, Ids.Company,
                assignedStaffId: null, RenewalTaskStatus.Submitted,
                serviceFeeAmount: null, completedAt: null, now));
        }

        if (!existingTaskIds.Contains(Ids.TaskAssigned))
        {
            db.RenewalTasks.Add(RenewalTask(
                Ids.TaskAssigned, Ids.AhmedVisa, Ids.Company,
                assignedStaffId: Ids.Agent, RenewalTaskStatus.Submitted,
                serviceFeeAmount: null, completedAt: null, now));
        }

        if (!existingTaskIds.Contains(Ids.TaskCompleted))
        {
            db.RenewalTasks.Add(RenewalTask(
                Ids.TaskCompleted, Ids.OmarEmiratesId, Ids.Company,
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
                InvoiceDate = DateOnly.FromDateTime(now.AddDays(-20)),
                DueDate = DateOnly.FromDateTime(now.AddDays(10)),
                PeriodStart = DateOnly.FromDateTime(now.AddDays(-30)),
                PeriodEnd = DateOnly.FromDateTime(now.AddDays(1)),
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
                InvoiceDate = DateOnly.FromDateTime(now.AddDays(-10)),
                DueDate = DateOnly.FromDateTime(now.AddDays(10)),
                Amount = 750m,
                Currency = "AED",
                Status = ServiceFeeInvoiceStatus.Pending,
                Description =
                    "Mock service fee invoice for completed Emirates ID renewal (E2E).",
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
}