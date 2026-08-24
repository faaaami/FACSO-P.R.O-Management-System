using MediatR;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoicePdf;

public sealed class GetRetainerInvoicePdfQueryHandler
    : IRequestHandler<
        GetRetainerInvoicePdfQuery,
        GetRetainerInvoicePdfResponseDto>
{
    private readonly IRetainerInvoiceReadRepository _readRepository;
    private readonly IInvoicePdfService _pdfService;

    public GetRetainerInvoicePdfQueryHandler(
        IRetainerInvoiceReadRepository readRepository,
        IInvoicePdfService pdfService)
    {
        _readRepository = readRepository;
        _pdfService = pdfService;
    }

    public async Task<GetRetainerInvoicePdfResponseDto> Handle(
        GetRetainerInvoicePdfQuery request,
        CancellationToken cancellationToken)
    {
        var invoice =
            await _readRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

        if (invoice is null)
        {
            throw new NotFoundException(
                "Retainer invoice was not found.");
        }

        var model = new InvoicePdfModel(
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.ClientCompanyName,
            invoice.ContractNumber,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.Amount,
            invoice.Currency,
            invoice.Status.ToString(),
            invoice.Notes);

        var pdfBytes =
            await _pdfService.GenerateAsync(
                model,
                cancellationToken);

        var fileName =
            $"retainer-invoice-{invoice.InvoiceNumber}.pdf";

        return new GetRetainerInvoicePdfResponseDto(
            pdfBytes,
            fileName);
    }
}
