using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(
        DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens
        => Set<EmailVerificationToken>();

    public DbSet<Notification> Notifications
        => Set<Notification>();

    public DbSet<PasswordResetToken> PasswordResetTokens
        => Set<PasswordResetToken>();

    public DbSet<ClientCompany> ClientCompanies
        => Set<ClientCompany>();

    public DbSet<ClientEntity> ClientEntities
        => Set<ClientEntity>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Document> Documents
    => Set<Document>();

    public DbSet<DocumentVersion> DocumentVersions
        => Set<DocumentVersion>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DocumentDependency> DocumentDependencies
    => Set<DocumentDependency>();
    public DbSet<DocumentExtraction> DocumentExtractions
    => Set<DocumentExtraction>();


    public DbSet<RenewalTask> RenewalTasks =>
    Set<RenewalTask>();

    public DbSet<RenewalStepLog> RenewalStepLogs =>
        Set<RenewalStepLog>();

    public DbSet<RenewalTaskHistory> RenewalTaskHistories =>
        Set<RenewalTaskHistory>();

    public DbSet<ServiceRequest> ServiceRequests =>
    Set<ServiceRequest>();

    public DbSet<ServiceContract> ServiceContracts =>
    Set<ServiceContract>();

    public DbSet<RetainerInvoice> RetainerInvoices =>
        Set<RetainerInvoice>();

    public DbSet<ServiceFeeInvoice> ServiceFeeInvoices =>
        Set<ServiceFeeInvoice>();

    public DbSet<GovFeeDisbursement> GovFeeDisbursements =>
        Set<GovFeeDisbursement>();

    public DbSet<PaymentOrder> PaymentOrders =>
        Set<PaymentOrder>();

    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    public DbSet<ServiceDefinition> ServiceDefinitions => Set<ServiceDefinition>();

    public DbSet<ServiceStepTemplate> ServiceStepTemplates => Set<ServiceStepTemplate>();

    public DbSet<ServiceRequiredDocument> ServiceRequiredDocuments => Set<ServiceRequiredDocument>();

    public DbSet<ContractServicePrice> ContractServicePrices => Set<ContractServicePrice>();

    public DbSet<ServiceJob> ServiceJobs => Set<ServiceJob>();

    public DbSet<ServiceJobStep> ServiceJobSteps => Set<ServiceJobStep>();

    public DbSet<ServiceJobDocument> ServiceJobDocuments => Set<ServiceJobDocument>();

    public DbSet<ServiceJobActivity> ServiceJobActivities => Set<ServiceJobActivity>();






    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>(SequenceNames.ServiceJobNumber);
        modelBuilder.HasSequence<long>(SequenceNames.CustomerCode);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(
                    entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(
                        nameof(ApplySoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(
                    null,
                    new object[] { modelBuilder });
            }
        }
    }

    private static void ApplySoftDeleteFilter<TEntity>(
        ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => !entity.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> NextSequenceValueAsync(
        string sequenceName,
        CancellationToken cancellationToken = default)
    {
        if (sequenceName is not (SequenceNames.ServiceJobNumber or SequenceNames.CustomerCode))
        {
            throw new ArgumentException("Unknown sequence.", nameof(sequenceName));
        }

        return await Database
            .SqlQueryRaw<long>($"SELECT nextval('\"{sequenceName}\"') AS \"Value\"")
            .SingleAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        return await Database.BeginTransactionAsync(cancellationToken);
    }
}