using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceJobs;

// ---------------------------------------------------------------- list

public sealed record GetServiceJobsQuery(
    ServiceJobStatus? Status,
    bool OpenOnly,
    Guid? ServiceDefinitionId,
    ServiceCategory? Category,
    Guid? ClientCompanyId,
    Guid? EmployeeId,
    Guid? AssignedStaffId,
    bool UnassignedOnly,
    bool OverdueOnly,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedList<ServiceJobListItemDto>>;

public sealed class GetServiceJobsQueryValidator : AbstractValidator<GetServiceJobsQuery>
{
    public GetServiceJobsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Category).IsInEnum().When(x => x.Category.HasValue);
    }
}

public sealed class GetServiceJobsQueryHandler
    : IRequestHandler<GetServiceJobsQuery, PaginatedList<ServiceJobListItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetServiceJobsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedList<ServiceJobListItemDto>> Handle(
        GetServiceJobsQuery request, CancellationToken cancellationToken)
    {
        var today = UaeClock.Today;
        var query = ServiceJobScope.Apply(_context.ServiceJobs.AsNoTracking(), _currentUser);

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }
        else if (request.OpenOnly)
        {
            query = query.Where(x => x.Status != ServiceJobStatus.Completed && x.Status != ServiceJobStatus.Cancelled);
        }

        if (request.ServiceDefinitionId.HasValue)
        {
            query = query.Where(x => x.ServiceDefinitionId == request.ServiceDefinitionId.Value);
        }

        if (request.Category.HasValue)
        {
            query = query.Where(x => x.ServiceDefinition!.Category == request.Category.Value);
        }

        if (request.ClientCompanyId.HasValue)
        {
            query = query.Where(x => x.ClientCompanyId == request.ClientCompanyId.Value);
        }

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(x => x.EmployeeId == request.EmployeeId.Value);
        }

        if (request.UnassignedOnly)
        {
            query = query.Where(x => x.AssignedStaffId == null);
        }
        else if (request.AssignedStaffId.HasValue)
        {
            query = query.Where(x => x.AssignedStaffId == request.AssignedStaffId.Value);
        }

        if (request.OverdueOnly)
        {
            query = query.Where(x => x.DueDate != null && x.DueDate < today &&
                                     x.Status != ServiceJobStatus.Completed &&
                                     x.Status != ServiceJobStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.JobNumber.ToLower().Contains(term) ||
                (x.ApplicationNumber != null && x.ApplicationNumber.ToLower().Contains(term)) ||
                x.ClientCompany!.CompanyName.ToLower().Contains(term) ||
                (x.ClientCompany.CustomerCode != null && x.ClientCompany.CustomerCode.ToLower().Contains(term)) ||
                x.ServiceDefinition!.Name.ToLower().Contains(term) ||
                (x.Employee != null && (x.Employee.FullName.ToLower().Contains(term) ||
                                        (x.Employee.PassportNumber != null &&
                                         x.Employee.PassportNumber.ToLower().Contains(term)))));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Status == ServiceJobStatus.Completed || x.Status == ServiceJobStatus.Cancelled)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ServiceJobListItemDto(
                x.Id, x.JobNumber, x.ClientCompanyId, x.ClientCompany!.CompanyName, x.ClientCompany.CustomerCode,
                x.ClientCompany.CustomerType, x.ServiceDefinitionId, x.ServiceDefinition!.Code,
                x.ServiceDefinition.Name, x.ServiceDefinition.Category,
                x.EmployeeId, x.Employee != null ? x.Employee.FullName : null,
                x.ClientEntityId, x.ClientEntity != null ? x.ClientEntity.EntityName : null,
                x.AssignedStaffId, x.AssignedStaff != null ? x.AssignedStaff.FullName : null,
                x.Status, x.Priority, x.Source,
                x.ServiceFee,
                x.ApplicationNumber, x.DueDate,
                x.DueDate != null && x.DueDate < today &&
                    x.Status != ServiceJobStatus.Completed && x.Status != ServiceJobStatus.Cancelled,
                x.Steps.Count(s => !s.IsDeleted),
                x.Steps.Count(s => !s.IsDeleted && s.Status != ServiceJobStepStatus.Pending),
                x.CreatedAt, x.CompletedAt))
            .ToListAsync(cancellationToken);

        return new PaginatedList<ServiceJobListItemDto>(items, total, request.Page, request.PageSize);
    }
}

// ---------------------------------------------------------------- detail

public sealed record GetServiceJobByIdQuery(Guid Id) : IRequest<ServiceJobDto>;

public sealed class GetServiceJobByIdQueryHandler : IRequestHandler<GetServiceJobByIdQuery, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetServiceJobByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServiceJobDto> Handle(GetServiceJobByIdQuery request, CancellationToken cancellationToken)
    {
        var job = await ServiceJobScope.Apply(_context.ServiceJobs.AsNoTracking(), _currentUser)
            .Include(x => x.ClientCompany)
            .Include(x => x.ServiceDefinition)
            .Include(x => x.Employee)
            .Include(x => x.ClientEntity)
            .Include(x => x.Document)
            .Include(x => x.AssignedStaff)
            .Include(x => x.Steps)
            .Include(x => x.Documents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Job was not found.");

        var isClient = ServiceJobScope.IsClient(_currentUser);

        var activities = await _context.ServiceJobActivities.AsNoTracking()
            .Where(x => x.ServiceJobId == job.Id && (!isClient || !x.IsInternal))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { Activity = x, UserName = _context.Users.Where(u => u.Id == x.UserId).Select(u => u.FullName).FirstOrDefault() })
            .ToListAsync(cancellationToken);

        var stepUserIds = job.Steps.Where(s => s.CompletedByUserId.HasValue).Select(s => s.CompletedByUserId!.Value).Distinct().ToList();
        var stepUsers = await _context.Users.AsNoTracking()
            .Where(u => stepUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var govFees = await _context.GovFeeDisbursements.AsNoTracking()
            .Where(x => x.ServiceJobId == job.Id)
            .OrderBy(x => x.PaidByFirmAt)
            .Select(x => new ServiceJobGovFeeDto(x.Id, x.FeeDescription, x.GovernmentReference, x.Amount, x.Status, x.PaidByFirmAt))
            .ToListAsync(cancellationToken);

        var today = UaeClock.Today;
        var isOpen = job.Status is not (ServiceJobStatus.Completed or ServiceJobStatus.Cancelled);

        return new ServiceJobDto(
            job.Id, job.JobNumber, job.ClientCompanyId, job.ClientCompany!.CompanyName, job.ClientCompany.CustomerCode,
            job.ClientCompany.CustomerType, job.ServiceDefinitionId, job.ServiceDefinition!.Code, job.ServiceDefinition.Name,
            job.ServiceDefinition.Category, job.ServiceDefinition.Authority,
            job.EmployeeId, job.Employee?.FullName, job.Employee?.PassportNumber,
            job.ClientEntityId, job.ClientEntity?.EntityName,
            job.DocumentId, job.Document?.DocumentNumber, job.Document?.Type, job.Document?.ExpiryDate,
            job.ServiceDefinition.RenewsDocumentType, job.ServiceRequestId,
            job.AssignedStaffId, job.AssignedStaff?.FullName,
            job.Status, job.Priority, job.Source,
            job.ServiceFee, job.ServiceFeeVatable, isClient ? null : job.EstimatedGovernmentFee,
            job.ApplicationNumber, job.DueDate,
            isOpen && job.DueDate is DateOnly due && due < today,
            isClient ? null : job.Notes,
            job.OnHoldReason, job.StartedAt, job.CompletedAt, job.CancelledAt, job.CancelReason,
            job.CreatedAt, job.UpdatedAt,
            job.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.Order)
                .Select(s => new ServiceJobStepDto(s.Id, s.Order, s.Name, s.Status,
                    s.ReferenceNumber, isClient ? null : s.Notes, s.CompletedAt, isClient ? null : s.CompletedByUserId,
                    s.CompletedByUserId is Guid u && stepUsers.TryGetValue(u, out var n) ? n : null))
                .ToList(),
            job.Documents.Where(d => !d.IsDeleted).OrderBy(d => d.Order)
                .Select(d => new ServiceJobDocumentDto(d.Id, d.Order, d.Name, d.IsMandatory, d.IsReceived, d.ReceivedAt))
                .ToList(),
            activities.Select(a => new ServiceJobActivityDto(a.Activity.Id, a.Activity.Action, a.Activity.FromStatus,
                    a.Activity.ToStatus, a.Activity.Note, a.Activity.IsInternal,
                    isClient ? null : a.Activity.UserId, a.UserName, a.Activity.CreatedAt))
                .ToList(),
            govFees);
    }
}

// ---------------------------------------------------------------- summary

public sealed record GetServiceJobSummaryQuery : IRequest<ServiceJobSummaryDto>;

public sealed class GetServiceJobSummaryQueryHandler : IRequestHandler<GetServiceJobSummaryQuery, ServiceJobSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetServiceJobSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServiceJobSummaryDto> Handle(GetServiceJobSummaryQuery request, CancellationToken cancellationToken)
    {
        var today = UaeClock.Today;
        // Start of today in UAE time, expressed in UTC (UAE is UTC+4, no DST).
        var todayStartUtc = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue).AddHours(-4), DateTimeKind.Utc);
        var jobs = ServiceJobScope.Apply(_context.ServiceJobs.AsNoTracking(), _currentUser);

        var counts = await jobs
            .GroupBy(_ => 1)
            .Select(g => new
            {
                New = g.Count(x => x.Status == ServiceJobStatus.New),
                InProgress = g.Count(x => x.Status == ServiceJobStatus.InProgress),
                OnHold = g.Count(x => x.Status == ServiceJobStatus.OnHold),
                CompletedToday = g.Count(x => x.Status == ServiceJobStatus.Completed && x.CompletedAt >= todayStartUtc),
                Overdue = g.Count(x => x.DueDate != null && x.DueDate < today &&
                                       x.Status != ServiceJobStatus.Completed && x.Status != ServiceJobStatus.Cancelled),
                Unassigned = g.Count(x => x.AssignedStaffId == null &&
                                          x.Status != ServiceJobStatus.Completed && x.Status != ServiceJobStatus.Cancelled)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (counts is null)
        {
            return new ServiceJobSummaryDto(0, 0, 0, 0, 0, 0, 0);
        }

        return new ServiceJobSummaryDto(counts.New, counts.InProgress, counts.OnHold, counts.CompletedToday,
            counts.Overdue, counts.Unassigned, counts.New + counts.InProgress + counts.OnHold);
    }
}
