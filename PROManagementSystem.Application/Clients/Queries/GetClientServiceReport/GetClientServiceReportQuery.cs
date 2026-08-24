using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientServiceReport;

public sealed record GetClientServiceReportQuery(
    Guid ClientCompanyId,
    DateTime? From,
    DateTime? To
) : IRequest<ClientServiceReportDto>;
