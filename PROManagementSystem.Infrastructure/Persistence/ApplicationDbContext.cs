using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PROManagementSystem.Application.Common.Interfaces;
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





    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        return await Database.BeginTransactionAsync(cancellationToken);
    }
}