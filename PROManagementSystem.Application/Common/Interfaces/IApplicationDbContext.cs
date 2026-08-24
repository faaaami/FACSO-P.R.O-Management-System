using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ClientCompany> ClientCompanies { get; }
    DbSet<ClientEntity> ClientEntities { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentVersion> DocumentVersions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<DocumentDependency> DocumentDependencies { get; }
    DbSet<DocumentExtraction> DocumentExtractions { get; }

    DbSet<RenewalTask> RenewalTasks { get; }

    DbSet<RenewalStepLog> RenewalStepLogs { get; }

    DbSet<RenewalTaskHistory> RenewalTaskHistories { get; }
    DbSet<ServiceRequest> ServiceRequests { get; }
    DbSet<ServiceContract> ServiceContracts { get; }
    DbSet<RetainerInvoice> RetainerInvoices { get; }
    DbSet<ServiceFeeInvoice> ServiceFeeInvoices { get; }
    DbSet<GovFeeDisbursement> GovFeeDisbursements { get; }
    DbSet<PaymentOrder> PaymentOrders { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}