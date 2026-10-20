using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.ServiceJobs;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequestToJob;

/// <summary>
/// Turns a client's service request into a job for any catalogue service
/// (new visa, cancellation, typing, ...). Unlike the renewal conversion it
/// does not need an existing document.
/// </summary>
public sealed record ConvertServiceRequestToJobCommand(
    Guid ServiceRequestId,
    Guid? ServiceDefinitionId,
    Guid? AssignedStaffId,
    ServiceJobPriority Priority,
    decimal? ServiceFee,
    DateOnly? DueDate,
    string? Notes) : IRequest<ConvertServiceRequestToJobResponseDto>;

public sealed record ConvertServiceRequestToJobResponseDto(
    Guid ServiceRequestId,
    Guid ServiceJobId,
    string JobNumber,
    ServiceRequestStatus Status,
    DateTime ConvertedAt);

public sealed class ConvertServiceRequestToJobCommandValidator : AbstractValidator<ConvertServiceRequestToJobCommand>
{
    public ConvertServiceRequestToJobCommandValidator()
    {
        RuleFor(x => x.ServiceRequestId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0).LessThan(10_000_000).When(x => x.ServiceFee.HasValue);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}

public sealed class ConvertServiceRequestToJobCommandHandler
    : IRequestHandler<ConvertServiceRequestToJobCommand, ConvertServiceRequestToJobResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceJobFactory _factory;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;

    public ConvertServiceRequestToJobCommandHandler(
        IApplicationDbContext context,
        IServiceJobFactory factory,
        ICurrentUserService currentUser,
        IUserNotifier notifier)
    {
        _context = context;
        _factory = factory;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<ConvertServiceRequestToJobResponseDto> Handle(
        ConvertServiceRequestToJobCommand request, CancellationToken cancellationToken)
    {
        ServiceRequest serviceRequest;
        ServiceJob job;

        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            // Same lock as convert-to-renewal and reject, so concurrent
            // decisions on one request are serialised.
            serviceRequest = (await _context.ServiceRequests
                    .FromSqlInterpolated(
                        $"SELECT * FROM service_requests WHERE \"Id\" = {request.ServiceRequestId} AND \"IsDeleted\" = false FOR UPDATE")
                    .ToListAsync(cancellationToken))
                .SingleOrDefault()
                ?? throw new NotFoundException("Service request was not found.");

            if (serviceRequest.Status != ServiceRequestStatus.Submitted)
            {
                throw new ConflictException("Only submitted service requests can be converted.");
            }

            var serviceId = request.ServiceDefinitionId ?? serviceRequest.ServiceDefinitionId
                ?? throw new ConflictException("Choose the service this request should become.");

            job = await _factory.CreateAsync(new NewServiceJob(
                serviceRequest.ClientCompanyId,
                serviceId,
                serviceRequest.EmployeeId,
                serviceRequest.EntityId,
                serviceRequest.DocumentId,
                request.AssignedStaffId,
                request.Priority,
                ServiceJobSource.ClientPortal,
                request.ServiceFee,
                request.DueDate,
                string.Join("\n", new[] { serviceRequest.Description, request.Notes }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                serviceRequest.Id), cancellationToken);

            var now = DateTime.UtcNow;
            serviceRequest.Status = ServiceRequestStatus.Converted;
            serviceRequest.ConvertedAt = now;
            serviceRequest.ConvertedServiceJobId = job.Id;
            serviceRequest.ServiceDefinitionId = serviceId;
            serviceRequest.UpdatedAt = now;

            _context.AddAudit(_currentUser.UserId, "ServiceRequestConverted", "ServiceRequest", serviceRequest.Id,
                $"Service request was converted to job {job.JobNumber}.");

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // After commit: a notification failure must not undo the decision.
        await _notifier.NotifyClientContactsAsync(serviceRequest.ClientCompanyId, "Service request accepted",
            $"Your request is now job {job.JobNumber}. You can follow its progress under Jobs.", cancellationToken);

        if (job.AssignedStaffId is Guid staffId)
        {
            await _notifier.NotifyUsersAsync([staffId], "Job assigned to you", $"{job.JobNumber} (from a client request).",
                cancellationToken);
        }

        return new ConvertServiceRequestToJobResponseDto(serviceRequest.Id, job.Id, job.JobNumber,
            serviceRequest.Status, serviceRequest.ConvertedAt!.Value);
    }
}
