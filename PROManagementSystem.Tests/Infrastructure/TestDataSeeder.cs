using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;

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

        public static readonly Guid NotificationA = Guid("0000000601");
        public static readonly Guid NotificationB = Guid("0000000602");

        public static readonly Guid Unknown = Guid("0000000999");

        private static Guid Guid(string hex)
            => System.Guid.Parse(
                $"00000000-0000-0000-0000-{hex.PadLeft(12, '0')}");
    }

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
                clientCompanyId: Ids.CompanyB));

        db.ClientCompanies.AddRange(
            Company(Ids.CompanyA, "Company A"),
            Company(Ids.CompanyB, "Company B"));

        db.ClientEntities.AddRange(
            Entity(Ids.EntityA, Ids.CompanyA, "Entity A"),
            Entity(Ids.EntityB, Ids.CompanyB, "Entity B"));

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
            TaskB(now));

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

        db.SaveChanges();
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
            PasswordHash = "seed-only-no-password",
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
}
