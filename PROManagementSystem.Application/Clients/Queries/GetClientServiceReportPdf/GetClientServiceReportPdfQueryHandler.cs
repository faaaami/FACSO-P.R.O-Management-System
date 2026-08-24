using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientServiceReport;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientServiceReportPdf;

public sealed class GetClientServiceReportPdfQueryHandler
    : IRequestHandler<GetClientServiceReportPdfQuery, byte[]>
{
    private readonly IClientServiceReportReadRepository _readRepository;
    private readonly IReportPdfService _pdfService;

    public GetClientServiceReportPdfQueryHandler(
        IClientServiceReportReadRepository readRepository,
        IReportPdfService pdfService)
    {
        _readRepository = readRepository;
        _pdfService = pdfService;
    }

    public async Task<byte[]> Handle(
        GetClientServiceReportPdfQuery request,
        CancellationToken cancellationToken)
    {
        var report = await _readRepository.GetReportAsync(
            request.ClientCompanyId,
            request.From,
            request.To,
            cancellationToken);

        return _pdfService.GenerateClientServiceReport(report);
    }
}
