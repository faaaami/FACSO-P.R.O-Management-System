using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Identity;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Persistence.Seeders;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Seeds a deterministic dataset of two independent "worlds" (A and B) so
/// that out-of-scope access can be asserted: Agent A only owns resources in
/// world A, Agent B only in world B. A shared collection fixture truncates
/// the tables between runs and reseeds.
/// </summary>
public static class TestDataSeeder
{
    public static class Ids
    {
        public static readonly Guid Admin = Guid("000000000001");
        public static readonly Guid AgentA = Guid("000000000002");
        public static readonly Guid AgentB = Guid("000000000003");
        public static readonly Guid ClientUserA = Guid("000000000004");
        public static readonly Guid ClientUserB = Guid("000000000005");

        public static readonly Guid InactiveUser = Guid("000000000006");
        public static readonly Guid NotApprovedUser = Guid("000000000007");
        public static readonly Guid NotVerifiedUser = Guid("000000000008");

        public static readonly Guid CompanyA = Guid("0000000101");
        public static readonly Guid CompanyB = Guid("0000000102");

        public static readonly Guid EntityA = Guid("0000000201");
        public static readonly Guid EntityB = Guid("0000000202");

        public static readonly Guid EmployeeA = Guid("0000000301");
        public static readonly Guid EmployeeB = Guid("0000000302");

        public static readonly Guid DocumentA = Guid("0000000401");
        public static readonly Guid DocumentB = Guid("0000000402");

        public static readonly Guid TaskA = Guid("0000000501");
        public static readonly Guid TaskB = Guid("0000000502");
        public static readonly Guid TaskC = Guid("0000000503");
        public static readonly Guid TaskD = Guid("0000000504");

        public static readonly Guid NotificationA = Guid("0000000601");
        public static readonly Guid NotificationB = Guid("0000000602");

        public static readonly Guid ServiceRequestA = Guid("0000000801");
        public static readonly Guid ServiceRequestB = Guid("0000000802");

        public static readonly Guid ContractA = Guid("0000000701");
        public static readonly Guid ContractB = Guid("0000000702");

        public static readonly Guid RetainerInvoiceA = Guid("0000000c01");
        public static readonly Guid RetainerInvoiceB = Guid("0000000c02");

        public static readonly Guid ServiceFeeInvoiceA = Guid("0000000d01");
        public static readonly Guid ServiceFeeInvoiceB = Guid("0000000d02");

        public static readonly Guid GovFeeDisbursementA = Guid("0000000e01");
        public static readonly Guid GovFeeDisbursementB = Guid("0000000e02");
        public static readonly Guid GovFeeDisbursementC = Guid("0000000e03");

        public static readonly Guid Unknown = Guid("0000000999");

        private static Guid Guid(string hex)
            => System.Guid.Parse(
                $"00000000-0000-0000-0000-{hex.PadLeft(12, '0')}");
    }

    /// <summary>Deterministic password used by login integration tests.</summary>
    public const string TestLoginPassword = "TestPassword123!";

    /// <summary>
    /// BCrypt hash of <see cref="TestLoginPassword"/>, generated once by the
    /// production hasher and reused across every seed run.
    /// </summary>
    private static readonly string TestLoginPasswordHash =
        new BCryptPasswordHasher().Hash(TestLoginPassword);

    public static void Seed(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;

        db.Users.AddRange(
            User(Ids.Admin, "System Admin", "admin@test.local", UserRole.Admin),
            User(Ids.AgentA, "Agent A", "agent-a@test.local", UserRole.Agent),
            User(Ids.AgentB, "Agent B", "agent-b@test.local", UserRole.Agent),
            User(Ids.ClientUserA, "Client A", "client-a@test.local", UserRole.Client,
                clientCompanyId: Ids.CompanyA),
            User(Ids.ClientUserB, "Client B", "client-b@test.local", UserRole.Client,
                clientCompanyId: Ids.CompanyB),
            User(Ids.InactiveUser, "Inactive User", "inactive@test.local",
                UserRole.Agent, isActive: false),
            User(Ids.NotApprovedUser, "Pending User", "pending@test.local",
                UserRole.Agent, isApproved: false),
            User(Ids.NotVerifiedUser, "Unverified User", "unverified@test.local",
                UserRole.Agent, emailVerified: false));

        db.ClientCompanies.AddRange(
            Company(Ids.CompanyA, "Company A"),
            Company(Ids.CompanyB, "Company B"));

        db.ClientEntities.AddRange(
            Entity(Ids.EntityA, Ids.CompanyA, "Entity A"),
            Entity(Ids.EntityB, Ids.CompanyB, "Entity B"));

        db.ServiceContracts.AddRange(
            Contract(Ids.ContractA, Ids.CompanyA, "CT-A-001"),
            Contract(Ids.ContractB, Ids.CompanyB, "CT-B-001"));

        db.Employees.AddRange(
            Employee(Ids.EmployeeA, Ids.EntityA, "Employee A"),
            Employee(Ids.EmployeeB, Ids.EntityB, "Employee B"));

        // Documents are linked to their owning employee so the agent-scoped
        // queries that join on AssignedStaffId -> Document resolve. Document
        // A expires within the expiring window; Document B is far out.
        db.Documents.AddRange(
            Document(Ids.DocumentA, employeeId: Ids.EmployeeA,
                "DOC-A-001",
                expiryDate: now.AddDays(10)),
            Document(Ids.DocumentB, employeeId: Ids.EmployeeB,
                "DOC-B-001",
                expiryDate: now.AddYears(4)));

        db.RenewalTasks.AddRange(
            TaskA(now),
            TaskB(now),
            TaskC(now),
            TaskD(now));

        db.ServiceRequests.AddRange(
            ServiceRequest(
                Ids.ServiceRequestA,
                Ids.CompanyA,
                ServiceRequestType.EarlyRenewal,
                Ids.EmployeeA,
                Ids.EntityA,
                Ids.DocumentA,
                "Early renewal service request for Company A."),
            ServiceRequest(
                Ids.ServiceRequestB,
                Ids.CompanyB,
                ServiceRequestType.Other,
                Ids.EmployeeB,
                Ids.EntityB,
                Ids.DocumentB,
                "Other service request for Company B."));

        db.Notifications.AddRange(
            new Notification
            {
                Id = Ids.NotificationA,
                UserId = Ids.AgentA,
                Title = "Inbox A",
                Message = "Notification for Agent A",
                IsRead = false,
                ReadAt = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            },
            new Notification
            {
                Id = Ids.NotificationB,
                UserId = Ids.AgentB,
                Title = "Inbox B",
                Message = "Notification for Agent B",
                IsRead = false,
                ReadAt = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

        db.RetainerInvoices.AddRange(
            RetainerInvoice(Ids.RetainerInvoiceA, Ids.CompanyA,
                Ids.ContractA, "INV-R-A-001",
                RetainerInvoiceStatus.Pending),
            RetainerInvoice(Ids.RetainerInvoiceB, Ids.CompanyB,
                Ids.ContractB, "INV-R-B-001",
                RetainerInvoiceStatus.Paid));

        db.ServiceFeeInvoices.AddRange(
            ServiceFeeInvoice(Ids.ServiceFeeInvoiceA, Ids.CompanyA,
                Ids.TaskC, "INV-SF-A-001",
                ServiceFeeInvoiceStatus.Pending),
            ServiceFeeInvoice(Ids.ServiceFeeInvoiceB, Ids.CompanyB,
                Ids.TaskD, "INV-SF-B-001",
                ServiceFeeInvoiceStatus.Void));

        db.GovFeeDisbursements.AddRange(
            GovFeeDisbursement(Ids.GovFeeDisbursementA, Ids.CompanyA,
                Ids.TaskA, "GOV-A-001",
                GovFeeDisbursementStatus.PaidByFirm),
            GovFeeDisbursement(Ids.GovFeeDisbursementB, Ids.CompanyB,
                Ids.TaskB, "GOV-B-001",
                GovFeeDisbursementStatus.InvoicedToClient),
            GovFeeDisbursement(Ids.GovFeeDisbursementC, Ids.CompanyA,
                Ids.TaskA, "GOV-A-002",
                GovFeeDisbursementStatus.Reimbursed));

        db.SaveChanges();

        // Append the realistic mock client company used for manual end-to-end
        // testing. Idempotent; runs after the A/B worlds are persisted so the
        // deterministic agent (Ids.AgentA) already exists for task assignment.
        MockClientCompanySeeder.Seed(db);
    }

    private static User User(
        Guid id,
        string fullName,
        string email,
        UserRole role,
        Guid? clientCompanyId = null,
        bool isActive = true,
        bool isApproved = true,
        bool emailVerified = true)
        => new()
        {
            Id = id,
            FullName = fullName,
            Email = email,
            PasswordHash = TestLoginPasswordHash,
            Role = role,
            IsActive = isActive,
            IsApproved = isApproved,
            EmailVerified = emailVerified,
            ClientCompanyId = clientCompanyId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static ClientCompany Company(Guid id, string name)
        => new()
        {
            Id = id,
            CompanyName = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static ClientEntity Entity(Guid id, Guid companyId, string name)
        => new()
        {
            Id = id,
            ClientCompanyId = companyId,
            EntityName = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static ServiceContract Contract(
        Guid id, Guid companyId, string contractNumber)
        => new()
        {
            Id = id,
            ClientCompanyId = companyId,
            ContractNumber = contractNumber,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Status = ServiceContractStatus.Active,
            RetainerAmount = 1000m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static Employee Employee(Guid id, Guid entityId, string fullName)
        => new()
        {
            Id = id,
            ClientEntityId = entityId,
            FullName = fullName,
            JobTitle = "Engineer",
            HireDate = DateTime.UtcNow.AddYears(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static Document Document(
        Guid id,
        Guid? employeeId,
        string docNumber,
        DateTime? expiryDate = null)
        => new()
        {
            Id = id,
            EmployeeId = employeeId,
            Type = DocumentType.Passport,
            DocumentNumber = docNumber,
            IssueDate = DateTime.UtcNow.AddYears(-1),
            ExpiryDate = expiryDate ?? DateTime.UtcNow.AddYears(4),
            FileUrl = "https://storage.test/dummy",
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            FileSize = 2048,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static RenewalTask TaskA(DateTime now)
        => RenewalTask(Ids.TaskA, Ids.DocumentA, Ids.CompanyA, Ids.AgentA,
            "TK-A-001", RenewalTaskStatus.Submitted);

    private static RenewalTask TaskB(DateTime now)
        => RenewalTask(Ids.TaskB, Ids.DocumentB, Ids.CompanyB, Ids.AgentB,
            "TK-B-001", RenewalTaskStatus.Submitted);

    private static RenewalTask TaskC(DateTime now)
        => RenewalTask(Ids.TaskC, Ids.DocumentA, Ids.CompanyA, Ids.AgentA,
            "TK-C-001", RenewalTaskStatus.Updated);

    private static RenewalTask TaskD(DateTime now)
        => RenewalTask(Ids.TaskD, Ids.DocumentB, Ids.CompanyB, Ids.AgentB,
            "TK-D-001", RenewalTaskStatus.Updated);

    private static RenewalTask RenewalTask(
        Guid id,
        Guid documentId,
        Guid companyId,
        Guid? agentId,
        string reference,
        RenewalTaskStatus status)
        => new()
        {
            Id = id,
            DocumentId = documentId,
            ClientCompanyId = companyId,
            AssignedStaffId = agentId,
            Status = status,
            ServiceFeeAmount = 150m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static ServiceRequest ServiceRequest(
        Guid id,
        Guid companyId,
        ServiceRequestType type,
        Guid? employeeId,
        Guid? entityId,
        Guid? documentId,
        string description)
        => new()
        {
            Id = id,
            ClientCompanyId = companyId,
            Type = type,
            EmployeeId = employeeId,
            EntityId = entityId,
            DocumentId = documentId,
            Status = ServiceRequestStatus.Submitted,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static RetainerInvoice RetainerInvoice(
        Guid id,
        Guid companyId,
        Guid contractId,
        string invoiceNumber,
        RetainerInvoiceStatus status)
        => new()
        {
            Id = id,
            ClientCompanyId = companyId,
            ServiceContractId = contractId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Amount = 2500m,
            Currency = "AED",
            Status = status,
            Notes = "Seeded retainer invoice.",
            PaidAt = status == RetainerInvoiceStatus.Paid
                ? DateTime.UtcNow.AddDays(-5)
                : null,
            VoidedAt = null,
            VoidReason = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static ServiceFeeInvoice ServiceFeeInvoice(
        Guid id,
        Guid companyId,
        Guid renewalTaskId,
        string invoiceNumber,
        ServiceFeeInvoiceStatus status)
        => new()
        {
            Id = id,
            ClientCompanyId = companyId,
            RenewalTaskId = renewalTaskId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Amount = 150m,
            Currency = "AED",
            Status = status,
            Description = "Seeded service fee invoice.",
            PaidAt = null,
            VoidedAt = status == ServiceFeeInvoiceStatus.Void
                ? DateTime.UtcNow.AddDays(-2)
                : null,
            VoidReason = status == ServiceFeeInvoiceStatus.Void
                ? "Seeded as void"
                : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    private static GovFeeDisbursement GovFeeDisbursement(
        Guid id,
        Guid companyId,
        Guid renewalTaskId,
        string governmentReference,
        GovFeeDisbursementStatus status)
        => new()
        {
            Id = id,
            ClientCompanyId = companyId,
            RenewalTaskId = renewalTaskId,
            FeeDescription = "Government fee",
            GovernmentReference = governmentReference,
            Amount = status == GovFeeDisbursementStatus.PaidByFirm
                ? 5000m
                : 750m,
            Currency = "AED",
            Status = status,
            PaidByFirmAt = DateTime.UtcNow.AddDays(-8),
            InvoicedAt = status >= GovFeeDisbursementStatus.InvoicedToClient
                ? DateTime.UtcNow.AddDays(-6)
                : null,
            ReimbursedAt = status == GovFeeDisbursementStatus.Reimbursed
                ? DateTime.UtcNow.AddDays(-1)
                : null,
            Notes = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
}
