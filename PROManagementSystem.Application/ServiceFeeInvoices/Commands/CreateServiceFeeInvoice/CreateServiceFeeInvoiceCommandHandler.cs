using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Notifications;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed class CreateServiceFeeInvoiceCommandHandler
    : IRequestHandler<
        CreateServiceFeeInvoiceCommand,
        CreateServiceFeeInvoiceResponseDto>
{
    private readonly IServiceFeeInvoiceCreationService
        _creationService;
    private readonly IUserNotifier _notifier;

    public CreateServiceFeeInvoiceCommandHandler(
        IServiceFeeInvoiceCreationService creationService,
        IUserNotifier notifier)
    {
        _creationService = creationService;
        _notifier = notifier;
    }

    public async Task<CreateServiceFeeInvoiceResponseDto> Handle(
        CreateServiceFeeInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var invoice =
            await _creationService.CreateAsync(
                request.ClientCompanyId,
                request.RenewalTaskId,
                request.Amount,
                request.Currency,
                request.InvoiceDate,
                request.DueDate,
                request.InvoiceNumber,
                request.Description,
                cancellationToken);

        // Only invoices an admin raises by hand notify here. The ones created
        // automatically when a renewal completes reach the client through the
        // completion notification instead.
        await _notifier.NotifyClientContactsAsync(
            invoice.ClientCompanyId,
            "New invoice",
            NotificationLabels.NewInvoice(
                invoice.InvoiceNumber,
                invoice.Amount,
                invoice.Currency,
                invoice.DueDate),
            cancellationToken);

        return new CreateServiceFeeInvoiceResponseDto(
            invoice.Id,
            invoice.ClientCompanyId,
            invoice.RenewalTaskId,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Amount,
            invoice.Currency,
            invoice.Status,
            invoice.Description,
            invoice.CreatedAt);
    }
}
