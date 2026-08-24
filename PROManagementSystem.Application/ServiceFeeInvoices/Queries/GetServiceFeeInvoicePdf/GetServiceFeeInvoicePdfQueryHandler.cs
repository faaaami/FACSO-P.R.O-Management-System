using MediatR;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoicePdf;

public sealed class GetServiceFeeInvoicePdfQueryHandler
    : IRequestHandler<
        GetServiceFeeInvoicePdfQuery,
        GetServiceFeeInvoicePdfResponseDto>
{
    private readonly IServiceFeeInvoiceReadRepository
        _readRepository;

    private readonly IInvoicePdfService _pdfService;

    public GetServiceFeeInvoicePdfQueryHandler(
        IServiceFeeInvoiceReadRepository readRepository,
        IInvoicePdfService pdfService)
    {
        _readRepository = readRepository;
        _pdfService = pdfService;
    }

    public async Task<GetServiceFeeInvoicePdfResponseDto> Handle(
        GetServiceFeeInvoicePdfQuery request,
        CancellationToken cancellationToken)
    {
        var invoice =
            await _readRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

        if (invoice is null)
        {
            throw new NotFoundException(
                "Service fee invoice was not found.");
        }

        var model = new InvoicePdfModel(
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.ClientCompanyName,
            null,
            null,
            null,
            invoice.Amount,
            invoice.Currency,
            invoice.Status.ToString(),
            invoice.Description,
            invoice.RenewalTaskId,
            "ServiceFee");

        var pdfBytes =
            await _pdfService.GenerateAsync(
                model,
                cancellationToken);

        var fileName =
            $"service-fee-invoice-{invoice.InvoiceNumber}.pdf";

        return new GetServiceFeeInvoicePdfResponseDto(
            pdfBytes,
            fileName);
    }
}
