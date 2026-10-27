using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceJobs;

/// <summary>
/// Row-level visibility of jobs: Admin sees all, an Agent only jobs assigned
/// to them, a Client only jobs of their own company. Anything outside the
/// scope is reported as not found, never as forbidden, so ids do not leak.
/// </summary>
internal static class ServiceJobScope
{
    public static IQueryable<ServiceJob> Apply(IQueryable<ServiceJob> query, ICurrentUserService user)
    {
        if (user.IsAdmin)
        {
            return query;
        }

        if (user.Role == nameof(UserRole.Agent) && user.UserId is Guid agentId)
        {
            return query.Where(x => x.AssignedStaffId == agentId);
        }

        if (user.Role == nameof(UserRole.Client) && user.ClientCompanyId is Guid companyId)
        {
            return query.Where(x => x.ClientCompanyId == companyId);
        }

        return query.Where(_ => false);
    }

    public static bool IsClient(ICurrentUserService user) => user.Role == nameof(UserRole.Client);

    public static void RequireStaff(ICurrentUserService user)
    {
        if (!user.IsAdmin && user.Role != nameof(UserRole.Agent))
        {
            throw new ForbiddenAccessException("Only staff can change jobs.");
        }
    }

    public static void RequireAdmin(ICurrentUserService user, string what)
    {
        if (!user.IsAdmin)
        {
            throw new ForbiddenAccessException($"Only an admin can {what}.");
        }
    }
}

/// <summary>Loads a job for modification under a row lock, within the caller's transaction.</summary>
internal static class ServiceJobLoader
{
    public static async Task<ServiceJob> LockAsync(
        IApplicationDbContext context,
        ICurrentUserService user,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        // Lock first, so two staff acting on the same job are serialised and
        // the second one re-reads the committed state before validating.
        var job = (await context.ServiceJobs
                .FromSqlInterpolated(
                    $"SELECT * FROM service_jobs WHERE \"Id\" = {jobId} AND \"IsDeleted\" = false FOR UPDATE")
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (job is null ||
            !await ServiceJobScope.Apply(context.ServiceJobs.AsNoTracking(), user)
                .AnyAsync(x => x.Id == jobId, cancellationToken))
        {
            throw new NotFoundException("Job was not found.");
        }

        // Tracked loads fix up the navigation collections of the locked entity.
        await context.ServiceJobSteps.Where(x => x.ServiceJobId == jobId).LoadAsync(cancellationToken);
        await context.ServiceJobDocuments.Where(x => x.ServiceJobId == jobId).LoadAsync(cancellationToken);
        await context.ServiceDefinitions.Where(x => x.Id == job.ServiceDefinitionId).LoadAsync(cancellationToken);

        return job;
    }

    public static void EnsureOpen(ServiceJob job)
    {
        if (job.Status is ServiceJobStatus.Completed or ServiceJobStatus.Cancelled)
        {
            throw new ConflictException($"Job {job.JobNumber} is {job.Status.ToString().ToLowerInvariant()} and can no longer be changed.");
        }
    }
}

internal static class ServiceJobActivities
{
    public static void Add(
        IApplicationDbContext context,
        ServiceJob job,
        Guid? userId,
        string action,
        string? note = null,
        ServiceJobStatus? from = null,
        ServiceJobStatus? to = null,
        bool isInternal = false)
    {
        var now = DateTime.UtcNow;
        context.ServiceJobActivities.Add(new ServiceJobActivity
        {
            Id = Guid.NewGuid(),
            ServiceJobId = job.Id,
            UserId = userId,
            Action = action,
            Note = note,
            FromStatus = from,
            ToStatus = to,
            IsInternal = isInternal,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}

/// <summary>Parameters of a new job (shared by direct creation and request conversion).</summary>
public sealed record NewServiceJob(
    Guid ClientCompanyId,
    Guid ServiceDefinitionId,
    Guid? EmployeeId,
    Guid? ClientEntityId,
    Guid? DocumentId,
    Guid? AssignedStaffId,
    ServiceJobPriority Priority,
    ServiceJobSource Source,
    decimal? ServiceFeeOverride,
    DateOnly? DueDate,
    string? Notes,
    Guid? ServiceRequestId);

/// <summary>
/// Validates a new job against the customer, subject and catalogue, prices it
/// and copies the service's step and document templates. Adds to the context
/// without saving, so callers control the transaction.
/// </summary>
public interface IServiceJobFactory
{
    Task<ServiceJob> CreateAsync(NewServiceJob input, CancellationToken cancellationToken);
}

public sealed class ServiceJobFactory : IServiceJobFactory
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ServiceJobFactory(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServiceJob> CreateAsync(NewServiceJob input, CancellationToken cancellationToken)
    {
        var company = await _context.ClientCompanies
            .FirstOrDefaultAsync(x => x.Id == input.ClientCompanyId, cancellationToken)
            ?? throw new NotFoundException("Client company was not found.");

        if (!company.IsActive)
        {
            throw new ConflictException("The customer is inactive.");
        }

        var service = await _context.ServiceDefinitions
            .Include(x => x.Steps)
            .Include(x => x.RequiredDocuments)
            .FirstOrDefaultAsync(x => x.Id == input.ServiceDefinitionId, cancellationToken)
            ?? throw new NotFoundException("Service was not found.");

        if (!service.IsActive)
        {
            throw new ConflictException($"The service '{service.Name}' is not offered any more.");
        }

        var (employeeId, entityId, documentId) = await ResolveSubjectAsync(input, service, cancellationToken);

        if (input.AssignedStaffId.HasValue)
        {
            await EnsureAssignableStaffAsync(_context, input.AssignedStaffId.Value, cancellationToken);
        }

        var fee = input.ServiceFeeOverride ?? await ResolveServiceFeeAsync(company.Id, service, cancellationToken);
        var now = DateTime.UtcNow;
        var number = await _context.NextSequenceValueAsync(SequenceNames.ServiceJobNumber, cancellationToken);

        var job = new ServiceJob
        {
            Id = Guid.NewGuid(),
            JobNumber = $"JOB-{UaeClock.Today.Year}-{number:D6}",
            ClientCompanyId = company.Id,
            ServiceDefinitionId = service.Id,
            EmployeeId = employeeId,
            ClientEntityId = entityId,
            DocumentId = documentId,
            ServiceRequestId = input.ServiceRequestId,
            AssignedStaffId = input.AssignedStaffId,
            Status = ServiceJobStatus.New,
            Priority = input.Priority,
            Source = input.Source,
            ServiceFee = decimal.Round(fee, 2),
            ServiceFeeVatable = service.ServiceFeeVatable,
            EstimatedGovernmentFee = service.EstimatedGovernmentFee,
            DueDate = input.DueDate ??
                      (service.EstimatedDays is int days ? UaeClock.Today.AddDays(days) : null),
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            CreatedByUserId = _currentUser.UserId ?? Guid.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var step in service.Steps.Where(x => !x.IsDeleted).OrderBy(x => x.Order))
        {
            job.Steps.Add(new ServiceJobStep
            {
                Id = Guid.NewGuid(), ServiceJobId = job.Id, Order = step.Order, Name = step.Name,
                CreatedAt = now, UpdatedAt = now
            });
        }

        foreach (var doc in service.RequiredDocuments.Where(x => !x.IsDeleted).OrderBy(x => x.Order))
        {
            job.Documents.Add(new ServiceJobDocument
            {
                Id = Guid.NewGuid(), ServiceJobId = job.Id, Order = doc.Order, Name = doc.Name,
                IsMandatory = doc.IsMandatory, CreatedAt = now, UpdatedAt = now
            });
        }

        _context.ServiceJobs.Add(job);

        ServiceJobActivities.Add(_context, job, _currentUser.UserId, "Created",
            $"{service.Name} for {company.CompanyName}.", to: ServiceJobStatus.New);

        _context.AddAudit(_currentUser.UserId, "ServiceJobCreated", "ServiceJob", job.Id,
            $"Job {job.JobNumber} ({service.Name}) was created for '{company.CompanyName}'.", employeeId);

        return job;
    }

    /// <summary>Contract price (or retainer inclusion) beats the catalogue default.</summary>
    private async Task<decimal> ResolveServiceFeeAsync(
        Guid clientCompanyId, ServiceDefinition service, CancellationToken cancellationToken)
    {
        var today = UaeClock.Today;

        var contractPrice = await _context.ContractServicePrices
            .Where(x => x.ServiceDefinitionId == service.Id &&
                        x.ServiceContract!.ClientCompanyId == clientCompanyId &&
                        x.ServiceContract.Status == ServiceContractStatus.Active &&
                        x.ServiceContract.StartDate <= today &&
                        (x.ServiceContract.EndDate == null || x.ServiceContract.EndDate >= today))
            .OrderByDescending(x => x.ServiceContract!.StartDate)
            .Select(x => (decimal?)(x.IncludedInRetainer ? 0 : x.ServiceFee))
            .FirstOrDefaultAsync(cancellationToken);

        return contractPrice ?? service.DefaultServiceFee;
    }

    private async Task<(Guid? EmployeeId, Guid? EntityId, Guid? DocumentId)> ResolveSubjectAsync(
        NewServiceJob input, ServiceDefinition service, CancellationToken cancellationToken)
    {
        Guid? employeeId = input.EmployeeId;
        Guid? entityId = input.ClientEntityId;
        Guid? documentId = input.DocumentId;

        if (documentId.HasValue)
        {
            var document = await _context.Documents.AsNoTracking()
                .Where(x => x.Id == documentId.Value)
                .Select(x => new
                {
                    x.EmployeeId,
                    x.ClientEntityId,
                    x.Type,
                    CompanyId = x.ClientEntityId != null
                        ? (Guid?)x.ClientEntity!.ClientCompanyId
                        : (Guid?)x.Employee!.ClientEntity.ClientCompanyId
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Document was not found.");

            if (document.CompanyId != input.ClientCompanyId)
            {
                throw new ConflictException("The document belongs to a different customer.");
            }

            if (service.RenewsDocumentType.HasValue && document.Type != service.RenewsDocumentType.Value)
            {
                throw new ConflictException(
                    $"'{service.Name}' renews a {service.RenewsDocumentType} but the selected document is a {document.Type}.");
            }

            employeeId ??= document.EmployeeId;
            entityId ??= document.ClientEntityId;
        }

        if (employeeId.HasValue)
        {
            var person = await _context.Employees.AsNoTracking()
                .Where(x => x.Id == employeeId.Value)
                .Select(x => new { x.ClientEntityId, x.ClientEntity.ClientCompanyId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Person was not found.");

            if (person.ClientCompanyId != input.ClientCompanyId)
            {
                throw new ConflictException("The person belongs to a different customer.");
            }

            entityId ??= person.ClientEntityId;
        }

        if (entityId.HasValue)
        {
            var entityCompanyId = await _context.ClientEntities.AsNoTracking()
                .Where(x => x.Id == entityId.Value)
                .Select(x => (Guid?)x.ClientCompanyId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Client entity was not found.");

            if (entityCompanyId != input.ClientCompanyId)
            {
                throw new ConflictException("The licence / entity belongs to a different customer.");
            }
        }
        else if (service.SubjectType == ServiceSubjectType.Company)
        {
            // A customer with a single licence needs no choice.
            var entities = await _context.ClientEntities.AsNoTracking()
                .Where(x => x.ClientCompanyId == input.ClientCompanyId)
                .Select(x => x.Id)
                .Take(2)
                .ToListAsync(cancellationToken);

            entityId = entities.Count == 1 ? entities[0] : null;
        }

        if (service.SubjectType == ServiceSubjectType.Person && !employeeId.HasValue)
        {
            throw new ConflictException($"'{service.Name}' is done for a person. Select the person.");
        }

        if (service.SubjectType == ServiceSubjectType.Company && !entityId.HasValue)
        {
            throw new ConflictException($"'{service.Name}' is done for a company. Select the licence / entity.");
        }

        return (employeeId, entityId, documentId);
    }

    internal static async Task<User> EnsureAssignableStaffAsync(
        IApplicationDbContext context, Guid staffId, CancellationToken cancellationToken)
    {
        var staff = await context.Users
            .FirstOrDefaultAsync(x => x.Id == staffId, cancellationToken)
            ?? throw new NotFoundException("Staff member was not found.");

        if (staff.Role is not (UserRole.Agent or UserRole.Admin))
        {
            throw new ConflictException("Jobs can only be assigned to staff (agents or admins).");
        }

        if (!staff.IsActive)
        {
            throw new ConflictException("The selected staff member is inactive.");
        }

        return staff;
    }
}
