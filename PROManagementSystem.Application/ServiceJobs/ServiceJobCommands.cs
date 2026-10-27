using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceJobs;

// ================================================================ create

public sealed record CreateServiceJobCommand(
    Guid ClientCompanyId,
    Guid ServiceDefinitionId,
    Guid? EmployeeId,
    Guid? ClientEntityId,
    Guid? DocumentId,
    Guid? AssignedStaffId,
    ServiceJobPriority Priority,
    ServiceJobSource Source,
    decimal? ServiceFee,
    DateOnly? DueDate,
    string? Notes) : IRequest<ServiceJobDto>;

public sealed class CreateServiceJobCommandValidator : AbstractValidator<CreateServiceJobCommand>
{
    public CreateServiceJobCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId).NotEmpty();
        RuleFor(x => x.ServiceDefinitionId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0).LessThan(10_000_000).When(x => x.ServiceFee.HasValue);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}

public sealed class CreateServiceJobCommandHandler : IRequestHandler<CreateServiceJobCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceJobFactory _factory;
    private readonly IUserNotifier _notifier;
    private readonly ISender _sender;

    public CreateServiceJobCommandHandler(
        IApplicationDbContext context, IServiceJobFactory factory, IUserNotifier notifier, ISender sender)
    {
        _context = context;
        _factory = factory;
        _notifier = notifier;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(CreateServiceJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _factory.CreateAsync(new NewServiceJob(
            request.ClientCompanyId, request.ServiceDefinitionId, request.EmployeeId, request.ClientEntityId,
            request.DocumentId, request.AssignedStaffId, request.Priority, request.Source, request.ServiceFee,
            request.DueDate, request.Notes, null), cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await ServiceJobNotifications.AssignedAsync(_context, _notifier, job, cancellationToken);

        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }
}

// ================================================================ assign

public sealed record AssignServiceJobCommand(Guid Id, Guid? AssignedStaffId) : IRequest<ServiceJobDto>;

public sealed class AssignServiceJobCommandHandler : IRequestHandler<AssignServiceJobCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;
    private readonly ISender _sender;

    public AssignServiceJobCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IUserNotifier notifier, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _notifier = notifier;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(AssignServiceJobCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireAdmin(_currentUser, "assign jobs");

        ServiceJob job;
        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            job = await ServiceJobLoader.LockAsync(_context, _currentUser, request.Id, cancellationToken);
            ServiceJobLoader.EnsureOpen(job);

            if (job.AssignedStaffId == request.AssignedStaffId)
            {
                return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
            }

            string note;
            if (request.AssignedStaffId is Guid staffId)
            {
                var staff = await ServiceJobFactory.EnsureAssignableStaffAsync(_context, staffId, cancellationToken);
                note = $"Assigned to {staff.FullName}.";
            }
            else
            {
                note = "Unassigned.";
            }

            job.AssignedStaffId = request.AssignedStaffId;
            job.UpdatedAt = DateTime.UtcNow;

            ServiceJobActivities.Add(_context, job, _currentUser.UserId, "Assigned", note, isInternal: true);
            _context.AddAudit(_currentUser.UserId, "ServiceJobAssigned", "ServiceJob", job.Id,
                $"Job {job.JobNumber}: {note}", job.EmployeeId);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await ServiceJobNotifications.AssignedAsync(_context, _notifier, job, cancellationToken);
        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }
}

// ================================================================ status transitions

public enum ServiceJobTransition
{
    Start = 1,
    Hold = 2,
    Resume = 3,
    Cancel = 4
}

public sealed record TransitionServiceJobCommand(Guid Id, ServiceJobTransition Transition, string? Reason)
    : IRequest<ServiceJobDto>;

public sealed class TransitionServiceJobCommandValidator : AbstractValidator<TransitionServiceJobCommand>
{
    public TransitionServiceJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Transition).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty()
            .When(x => x.Transition is ServiceJobTransition.Hold or ServiceJobTransition.Cancel)
            .WithMessage("A reason is required.");
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public sealed class TransitionServiceJobCommandHandler : IRequestHandler<TransitionServiceJobCommand, ServiceJobDto>
{
    private static readonly Dictionary<ServiceJobTransition, (ServiceJobStatus[] From, ServiceJobStatus To)> Rules = new()
    {
        [ServiceJobTransition.Start] = ([ServiceJobStatus.New], ServiceJobStatus.InProgress),
        [ServiceJobTransition.Hold] = ([ServiceJobStatus.New, ServiceJobStatus.InProgress], ServiceJobStatus.OnHold),
        [ServiceJobTransition.Resume] = ([ServiceJobStatus.OnHold], ServiceJobStatus.InProgress),
        [ServiceJobTransition.Cancel] = (
            [ServiceJobStatus.New, ServiceJobStatus.InProgress, ServiceJobStatus.OnHold], ServiceJobStatus.Cancelled)
    };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;
    private readonly ISender _sender;

    public TransitionServiceJobCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IUserNotifier notifier, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _notifier = notifier;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(TransitionServiceJobCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireStaff(_currentUser);

        if (request.Transition == ServiceJobTransition.Cancel)
        {
            ServiceJobScope.RequireAdmin(_currentUser, "cancel jobs");
        }

        var (from, to) = Rules[request.Transition];
        ServiceJob job;
        ServiceJobStatus previous;

        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            job = await ServiceJobLoader.LockAsync(_context, _currentUser, request.Id, cancellationToken);
            previous = job.Status;

            if (!from.Contains(job.Status))
            {
                throw new ConflictException(
                    $"Job {job.JobNumber} is {job.Status} and cannot be {Verb(request.Transition)}.");
            }

            var now = DateTime.UtcNow;
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            job.Status = to;
            job.UpdatedAt = now;
            job.StartedAt ??= to == ServiceJobStatus.InProgress ? now : null;
            job.OnHoldReason = to == ServiceJobStatus.OnHold ? reason : null;

            if (to == ServiceJobStatus.Cancelled)
            {
                job.CancelledAt = now;
                job.CancelReason = reason;
            }

            ServiceJobActivities.Add(_context, job, _currentUser.UserId, request.Transition.ToString(), reason, previous, to);
            _context.AddAudit(_currentUser.UserId, "ServiceJobStatusChanged", "ServiceJob", job.Id,
                $"Job {job.JobNumber} moved from {previous} to {to}." + (reason is null ? string.Empty : $" Reason: {reason}"),
                job.EmployeeId);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await ServiceJobNotifications.StatusChangedAsync(_context, _notifier, job, cancellationToken);
        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }

    private static string Verb(ServiceJobTransition transition) => transition switch
    {
        ServiceJobTransition.Start => "started",
        ServiceJobTransition.Hold => "put on hold",
        ServiceJobTransition.Resume => "resumed",
        _ => "cancelled"
    };
}

// ================================================================ complete

public sealed record CompleteServiceJobCommand(
    Guid Id,
    string? Note,
    string? RenewedDocumentNumber,
    DateTime? RenewedIssueDate,
    DateTime? RenewedExpiryDate) : IRequest<ServiceJobDto>;

public sealed class CompleteServiceJobCommandValidator : AbstractValidator<CompleteServiceJobCommand>
{
    public CompleteServiceJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.RenewedDocumentNumber).MaximumLength(100);
        RuleFor(x => x.RenewedIssueDate)
            .LessThanOrEqualTo(_ => UaeClock.Now.Date.AddDays(1))
            .When(x => x.RenewedIssueDate.HasValue)
            .WithMessage("Issue date cannot be in the future.");
    }
}

public sealed class CompleteServiceJobCommandHandler : IRequestHandler<CompleteServiceJobCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;
    private readonly ISender _sender;

    public CompleteServiceJobCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IUserNotifier notifier, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _notifier = notifier;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(CompleteServiceJobCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireStaff(_currentUser);
        ServiceJob job;

        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            job = await ServiceJobLoader.LockAsync(_context, _currentUser, request.Id, cancellationToken);

            if (job.Status != ServiceJobStatus.InProgress)
            {
                throw new ConflictException(
                    $"Only a job in progress can be completed. Job {job.JobNumber} is {job.Status}.");
            }

            var pending = job.Steps.Where(x => !x.IsDeleted && x.Status == ServiceJobStepStatus.Pending)
                .Select(x => x.Name).ToList();

            if (pending.Count > 0)
            {
                throw new ConflictException(
                    $"Finish or skip every step first. Pending: {string.Join(", ", pending)}.");
            }

            var now = DateTime.UtcNow;
            var renewal = await ApplyRenewalAsync(job, request, now, cancellationToken);

            job.Status = ServiceJobStatus.Completed;
            job.CompletedAt = now;
            job.OnHoldReason = null;
            job.UpdatedAt = now;

            var note = string.Join(" ", new[] { request.Note?.Trim(), renewal }.Where(x => !string.IsNullOrEmpty(x)));
            ServiceJobActivities.Add(_context, job, _currentUser.UserId, "Completed",
                note.Length == 0 ? null : note, ServiceJobStatus.InProgress, ServiceJobStatus.Completed);
            _context.AddAudit(_currentUser.UserId, "ServiceJobCompleted", "ServiceJob", job.Id,
                $"Job {job.JobNumber} was completed. {renewal}".Trim(), job.EmployeeId);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await ServiceJobNotifications.StatusChangedAsync(_context, _notifier, job, cancellationToken);
        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }

    /// <summary>
    /// A renewal job carries the renewed document's new expiry (and optionally
    /// number and issue date) onto the linked document.
    /// </summary>
    private async Task<string?> ApplyRenewalAsync(
        ServiceJob job, CompleteServiceJobCommand request, DateTime now, CancellationToken cancellationToken)
    {
        var renews = job.ServiceDefinition?.RenewsDocumentType;

        if (renews is null || job.DocumentId is null)
        {
            return null;
        }

        if (request.RenewedExpiryDate is not DateTime expiry)
        {
            throw new ConflictException("Enter the new expiry date of the renewed document.");
        }

        if (DateOnly.FromDateTime(expiry) <= UaeClock.Today)
        {
            throw new ConflictException("The new expiry date must be in the future.");
        }

        var document = await _context.Documents
            .FirstOrDefaultAsync(x => x.Id == job.DocumentId.Value, cancellationToken)
            ?? throw new NotFoundException("The linked document no longer exists.");

        var previous = document.ExpiryDate;
        document.ExpiryDate = DateTime.SpecifyKind(expiry.Date, DateTimeKind.Utc);

        if (!string.IsNullOrWhiteSpace(request.RenewedDocumentNumber))
        {
            document.DocumentNumber = request.RenewedDocumentNumber.Trim();
        }

        if (request.RenewedIssueDate is DateTime issued)
        {
            document.IssueDate = DateTime.SpecifyKind(issued.Date, DateTimeKind.Utc);
        }

        document.UpdatedAt = now;

        _context.AddAudit(_currentUser.UserId, "DocumentRenewed", "Document", document.Id,
            $"Renewed through job {job.JobNumber}. Expiry {previous:yyyy-MM-dd} → {document.ExpiryDate:yyyy-MM-dd}.",
            document.EmployeeId);

        return $"{document.Type} {document.DocumentNumber} now expires on {document.ExpiryDate:yyyy-MM-dd}.";
    }
}

// ================================================================ details

public sealed record UpdateServiceJobDetailsCommand(
    Guid Id,
    string? ApplicationNumber,
    string? Notes,
    DateOnly? DueDate,
    ServiceJobPriority? Priority,
    decimal? ServiceFee) : IRequest<ServiceJobDto>;

public sealed class UpdateServiceJobDetailsCommandValidator : AbstractValidator<UpdateServiceJobDetailsCommand>
{
    public UpdateServiceJobDetailsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ApplicationNumber).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0).LessThan(10_000_000).When(x => x.ServiceFee.HasValue);
    }
}

public sealed class UpdateServiceJobDetailsCommandHandler : IRequestHandler<UpdateServiceJobDetailsCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public UpdateServiceJobDetailsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(UpdateServiceJobDetailsCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireStaff(_currentUser);

        if (!_currentUser.IsAdmin && (request.DueDate.HasValue || request.Priority.HasValue || request.ServiceFee.HasValue))
        {
            throw new ForbiddenAccessException("Only an admin can change the due date, priority or fee.");
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        var job = await ServiceJobLoader.LockAsync(_context, _currentUser, request.Id, cancellationToken);
        ServiceJobLoader.EnsureOpen(job);

        var changes = new List<string>();
        var applicationNumber = string.IsNullOrWhiteSpace(request.ApplicationNumber) ? null : request.ApplicationNumber.Trim();
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (applicationNumber != job.ApplicationNumber)
        {
            changes.Add($"application number {job.ApplicationNumber ?? "-"} → {applicationNumber ?? "-"}");
            job.ApplicationNumber = applicationNumber;
        }

        if (notes != job.Notes)
        {
            changes.Add("notes");
            job.Notes = notes;
        }

        if (_currentUser.IsAdmin)
        {
            if (request.DueDate != job.DueDate)
            {
                changes.Add($"due date {job.DueDate?.ToString("yyyy-MM-dd") ?? "-"} → {request.DueDate?.ToString("yyyy-MM-dd") ?? "-"}");
                job.DueDate = request.DueDate;
            }

            if (request.Priority is ServiceJobPriority priority && priority != job.Priority)
            {
                changes.Add($"priority {job.Priority} → {priority}");
                job.Priority = priority;
            }

            if (request.ServiceFee is decimal fee && decimal.Round(fee, 2) != job.ServiceFee)
            {
                changes.Add($"service fee {job.ServiceFee:0.00} → {fee:0.00}");
                job.ServiceFee = decimal.Round(fee, 2);
            }
        }

        if (changes.Count > 0)
        {
            job.UpdatedAt = DateTime.UtcNow;
            var summary = "Updated " + string.Join(", ", changes) + ".";
            ServiceJobActivities.Add(_context, job, _currentUser.UserId, "Updated", summary, isInternal: true);
            _context.AddAudit(_currentUser.UserId, "ServiceJobUpdated", "ServiceJob", job.Id,
                $"Job {job.JobNumber}: {summary}", job.EmployeeId);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }
}

// ================================================================ steps

public sealed record UpdateServiceJobStepCommand(
    Guid JobId,
    Guid StepId,
    ServiceJobStepStatus Status,
    string? ReferenceNumber,
    string? Notes) : IRequest<ServiceJobDto>;

public sealed class UpdateServiceJobStepCommandValidator : AbstractValidator<UpdateServiceJobStepCommand>
{
    public UpdateServiceJobStepCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.StepId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class UpdateServiceJobStepCommandHandler : IRequestHandler<UpdateServiceJobStepCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public UpdateServiceJobStepCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(UpdateServiceJobStepCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireStaff(_currentUser);

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        var job = await ServiceJobLoader.LockAsync(_context, _currentUser, request.JobId, cancellationToken);
        ServiceJobLoader.EnsureOpen(job);

        if (job.Status == ServiceJobStatus.OnHold)
        {
            throw new ConflictException($"Job {job.JobNumber} is on hold. Resume it before updating steps.");
        }

        var step = job.Steps.FirstOrDefault(x => x.Id == request.StepId && !x.IsDeleted)
            ?? throw new NotFoundException("Step was not found.");

        var now = DateTime.UtcNow;
        var previousStatus = job.Status;

        step.Status = request.Status;
        step.ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();
        step.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        step.CompletedAt = request.Status == ServiceJobStepStatus.Pending ? null : now;
        step.CompletedByUserId = request.Status == ServiceJobStepStatus.Pending ? null : _currentUser.UserId;
        step.UpdatedAt = now;

        // Working on a step means the job has started.
        if (job.Status == ServiceJobStatus.New && request.Status != ServiceJobStepStatus.Pending)
        {
            job.Status = ServiceJobStatus.InProgress;
            job.StartedAt ??= now;
        }

        job.UpdatedAt = now;

        var text = request.Status switch
        {
            ServiceJobStepStatus.Done => $"Step '{step.Name}' done.",
            ServiceJobStepStatus.Skipped => $"Step '{step.Name}' skipped.",
            _ => $"Step '{step.Name}' reopened."
        };

        if (step.ReferenceNumber is not null)
        {
            text += $" Ref: {step.ReferenceNumber}.";
        }

        ServiceJobActivities.Add(_context, job, _currentUser.UserId, "Step", text,
            previousStatus != job.Status ? previousStatus : null,
            previousStatus != job.Status ? job.Status : null);
        _context.AddAudit(_currentUser.UserId, "ServiceJobStepUpdated", "ServiceJob", job.Id,
            $"Job {job.JobNumber}: {text}", job.EmployeeId);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }
}

// ================================================================ document checklist

public sealed record SetServiceJobDocumentReceivedCommand(Guid JobId, Guid DocumentId, bool IsReceived)
    : IRequest<ServiceJobDto>;

public sealed class SetServiceJobDocumentReceivedCommandHandler
    : IRequestHandler<SetServiceJobDocumentReceivedCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public SetServiceJobDocumentReceivedCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(SetServiceJobDocumentReceivedCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireStaff(_currentUser);

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        var job = await ServiceJobLoader.LockAsync(_context, _currentUser, request.JobId, cancellationToken);
        ServiceJobLoader.EnsureOpen(job);

        var document = job.Documents.FirstOrDefault(x => x.Id == request.DocumentId && !x.IsDeleted)
            ?? throw new NotFoundException("Checklist item was not found.");

        if (document.IsReceived != request.IsReceived)
        {
            var now = DateTime.UtcNow;
            document.IsReceived = request.IsReceived;
            document.ReceivedAt = request.IsReceived ? now : null;
            document.UpdatedAt = now;
            job.UpdatedAt = now;

            ServiceJobActivities.Add(_context, job, _currentUser.UserId, "Document",
                request.IsReceived ? $"Received: {document.Name}." : $"Marked not received: {document.Name}.");
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }
}

// ================================================================ notes

public sealed record AddServiceJobNoteCommand(Guid JobId, string Note, bool IsInternal) : IRequest<ServiceJobDto>;

public sealed class AddServiceJobNoteCommandValidator : AbstractValidator<AddServiceJobNoteCommand>
{
    public AddServiceJobNoteCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
    }
}

public sealed class AddServiceJobNoteCommandHandler : IRequestHandler<AddServiceJobNoteCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;
    private readonly ISender _sender;

    public AddServiceJobNoteCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IUserNotifier notifier, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _notifier = notifier;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(AddServiceJobNoteCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireStaff(_currentUser);

        var job = await ServiceJobScope.Apply(_context.ServiceJobs, _currentUser)
            .FirstOrDefaultAsync(x => x.Id == request.JobId, cancellationToken)
            ?? throw new NotFoundException("Job was not found.");

        ServiceJobActivities.Add(_context, job, _currentUser.UserId, "Note", request.Note.Trim(), isInternal: request.IsInternal);
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        if (!request.IsInternal)
        {
            await _notifier.NotifyClientContactsAsync(job.ClientCompanyId, $"Update on {job.JobNumber}",
                request.Note.Trim(), cancellationToken);
        }

        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }
}

// ================================================================ government fees

public sealed record RecordServiceJobGovFeeCommand(
    Guid JobId,
    string FeeDescription,
    string? GovernmentReference,
    decimal Amount,
    DateTime? PaidAt,
    string? Notes) : IRequest<ServiceJobDto>;

public sealed class RecordServiceJobGovFeeCommandValidator : AbstractValidator<RecordServiceJobGovFeeCommand>
{
    public RecordServiceJobGovFeeCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.FeeDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.GovernmentReference).MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(10_000_000);
        RuleFor(x => x.PaidAt).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).When(x => x.PaidAt.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class RecordServiceJobGovFeeCommandHandler : IRequestHandler<RecordServiceJobGovFeeCommand, ServiceJobDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public RecordServiceJobGovFeeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<ServiceJobDto> Handle(RecordServiceJobGovFeeCommand request, CancellationToken cancellationToken)
    {
        ServiceJobScope.RequireStaff(_currentUser);

        var job = await ServiceJobScope.Apply(_context.ServiceJobs, _currentUser)
            .FirstOrDefaultAsync(x => x.Id == request.JobId, cancellationToken)
            ?? throw new NotFoundException("Job was not found.");

        if (job.Status == ServiceJobStatus.Cancelled)
        {
            throw new ConflictException("Government fees cannot be recorded on a cancelled job.");
        }

        var now = DateTime.UtcNow;
        var fee = new GovFeeDisbursement
        {
            Id = Guid.NewGuid(),
            ClientCompanyId = job.ClientCompanyId,
            ServiceJobId = job.Id,
            FeeDescription = request.FeeDescription.Trim(),
            GovernmentReference = string.IsNullOrWhiteSpace(request.GovernmentReference) ? null : request.GovernmentReference.Trim(),
            Amount = decimal.Round(request.Amount, 2),
            Currency = "AED",
            Status = GovFeeDisbursementStatus.PaidByFirm,
            PaidByFirmAt = request.PaidAt.HasValue ? DateTime.SpecifyKind(request.PaidAt.Value, DateTimeKind.Utc) : now,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.GovFeeDisbursements.Add(fee);

        job.UpdatedAt = now;
        ServiceJobActivities.Add(_context, job, _currentUser.UserId, "GovernmentFee",
            $"Government fee paid: {fee.FeeDescription}, AED {fee.Amount:0.00}" +
            (fee.GovernmentReference is null ? "." : $" (ref {fee.GovernmentReference})."));
        _context.AddAudit(_currentUser.UserId, "GovFeeDisbursementCreated", "GovFeeDisbursement", fee.Id,
            $"Government fee AED {fee.Amount:0.00} recorded on job {job.JobNumber}.", job.EmployeeId);

        await _context.SaveChangesAsync(cancellationToken);
        return await _sender.Send(new GetServiceJobByIdQuery(job.Id), cancellationToken);
    }
}

// ================================================================ notifications

internal static class ServiceJobNotifications
{
    public static async Task AssignedAsync(
        IApplicationDbContext context, IUserNotifier notifier, ServiceJob job, CancellationToken cancellationToken)
    {
        if (job.AssignedStaffId is not Guid staffId)
        {
            return;
        }

        var serviceName = await ServiceNameAsync(context, job, cancellationToken);
        await notifier.NotifyUsersAsync([staffId], "Job assigned to you",
            $"{job.JobNumber}: {serviceName}.", cancellationToken);
    }

    public static async Task StatusChangedAsync(
        IApplicationDbContext context, IUserNotifier notifier, ServiceJob job, CancellationToken cancellationToken)
    {
        var serviceName = await ServiceNameAsync(context, job, cancellationToken);

        var (title, message) = job.Status switch
        {
            ServiceJobStatus.InProgress => ("Work started", $"We have started on {job.JobNumber}: {serviceName}."),
            ServiceJobStatus.OnHold => ("Action may be needed", $"{job.JobNumber}: {serviceName} is on hold. {job.OnHoldReason}"),
            ServiceJobStatus.Completed => ("Service completed", $"{job.JobNumber}: {serviceName} is complete."),
            ServiceJobStatus.Cancelled => ("Service cancelled", $"{job.JobNumber}: {serviceName} was cancelled. {job.CancelReason}"),
            _ => (null, null)
        };

        if (title is not null)
        {
            await notifier.NotifyClientContactsAsync(job.ClientCompanyId, title, message!.Trim(), cancellationToken);
        }
    }

    private static Task<string> ServiceNameAsync(
        IApplicationDbContext context, ServiceJob job, CancellationToken cancellationToken) =>
        job.ServiceDefinition is not null
            ? Task.FromResult(job.ServiceDefinition.Name)
            : context.ServiceDefinitions.Where(x => x.Id == job.ServiceDefinitionId)
                .Select(x => x.Name).FirstAsync(cancellationToken);
}
