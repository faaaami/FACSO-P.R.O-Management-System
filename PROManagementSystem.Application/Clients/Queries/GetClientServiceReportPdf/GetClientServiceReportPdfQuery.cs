using MediatR;

namespace PROManagementSystem.Application.Clients.Queries.GetClientServiceReportPdf;

public sealed record GetClientServiceReportPdfQuery(
    Guid ClientCompanyId,
    DateTime? From,
    DateTime? To
) : IRequest<byte[]>;
