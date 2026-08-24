using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed class CreateServiceFeeInvoiceCommandHandler
    : IRequestHandler<
        CreateServiceFeeInvoiceCommand,
        CreateServiceFeeInvoiceResponseDto>
{
    private readonly IServiceFeeInvoiceCreationService
        _creationService;

    public CreateServiceFeeInvoiceCommandHandler(
        IServiceFeeInvoiceCreationService creationService)
    {
        _creationService = creationService;
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
