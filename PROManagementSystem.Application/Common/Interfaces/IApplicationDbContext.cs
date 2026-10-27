using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    DbSet<WebhookEvent> WebhookEvents { get; }
    DbSet<ServiceDefinition> ServiceDefinitions { get; }
    DbSet<ServiceStepTemplate> ServiceStepTemplates { get; }
    DbSet<ServiceRequiredDocument> ServiceRequiredDocuments { get; }
    DbSet<ContractServicePrice> ContractServicePrices { get; }
    DbSet<ServiceJob> ServiceJobs { get; }
    DbSet<ServiceJobStep> ServiceJobSteps { get; }
    DbSet<ServiceJobDocument> ServiceJobDocuments { get; }
    DbSet<ServiceJobActivity> ServiceJobActivities { get; }

    /// <summary>
    /// Next value of a Postgres sequence; used for gap-tolerant, never-reused
    /// human readable numbers (job numbers, customer codes).
    /// </summary>
    Task<long> NextSequenceValueAsync(
        string sequenceName,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction. Used by financial flows that
    /// must commit multiple entity changes atomically and serialise
    /// concurrent row-level updates (e.g. webhook payment completion).
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
}