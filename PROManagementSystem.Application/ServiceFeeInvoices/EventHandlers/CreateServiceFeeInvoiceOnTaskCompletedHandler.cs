using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.ServiceFeeInvoices.EventHandlers;

public sealed class CreateServiceFeeInvoiceOnTaskCompletedHandler
    : INotificationHandler<RenewalTaskCompletedEvent>
{
    private readonly IServiceFeeInvoiceCreationService
        _creationService;

    private readonly IApplicationDbContext _context;

    private readonly ILogger<
        CreateServiceFeeInvoiceOnTaskCompletedHandler>
        _logger;

    public CreateServiceFeeInvoiceOnTaskCompletedHandler(
        IServiceFeeInvoiceCreationService creationService,
        IApplicationDbContext context,
        ILogger<CreateServiceFeeInvoiceOnTaskCompletedHandler>
            logger)
    {
        _creationService = creationService;
        _context = context;
        _logger = logger;
    }

    public async Task Handle(
        RenewalTaskCompletedEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing RenewalTaskCompletedEvent for task {TaskId}.",
            notification.RenewalTaskId);

        var task =
            await _context.RenewalTasks
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            notification.RenewalTaskId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (task is null)
        {
            _logger.LogError(
                "RenewalTask {TaskId} not found when processing completion event.",
                notification.RenewalTaskId);

            throw new NotFoundException(
                "Renewal task was not found.");
        }

        if (!task.ServiceFeeAmount.HasValue ||
            task.ServiceFeeAmount.Value <= 0)
        {
            _logger.LogError(
                "RenewalTask {TaskId} has invalid ServiceFeeAmount ({Amount}) at completion.",
                task.Id,
                task.ServiceFeeAmount);

            throw new ConflictException(
                "Cannot create service fee invoice: RenewalTask.ServiceFeeAmount must be greater than 0.");
        }

        var invoiceNumber =
            $"SFI-{DateTime.UtcNow:yyyyMMdd}-{task.Id.ToString()[..8].ToUpper()}";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            var invoice =
                await _creationService.CreateAsync(
                    notification.ClientCompanyId,
                    notification.RenewalTaskId,
                    task.ServiceFeeAmount.Value,
                    "AED",
                    today,
                    today.AddDays(30),
                    invoiceNumber,
                    $"Service fee for completed renewal task {task.Id}.",
                    cancellationToken);

            _logger.LogInformation(
                "ServiceFeeInvoice {InvoiceId} created automatically for RenewalTask {TaskId}.",
                invoice.Id,
                notification.RenewalTaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create ServiceFeeInvoice automatically for RenewalTask {TaskId}.",
                notification.RenewalTaskId);

            throw;
        }
    }
}
