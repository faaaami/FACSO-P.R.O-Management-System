using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientServiceReport;

public sealed class GetClientServiceReportQueryHandler
    : IRequestHandler<GetClientServiceReportQuery, ClientServiceReportDto>
{
    private readonly IClientServiceReportReadRepository _readRepository;

    public GetClientServiceReportQueryHandler(
        IClientServiceReportReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ClientServiceReportDto> Handle(
        GetClientServiceReportQuery request,
        CancellationToken cancellationToken)
    {
        return await _readRepository.GetReportAsync(
            request.ClientCompanyId,
            request.From,
            request.To,
            cancellationToken);
    }
}
