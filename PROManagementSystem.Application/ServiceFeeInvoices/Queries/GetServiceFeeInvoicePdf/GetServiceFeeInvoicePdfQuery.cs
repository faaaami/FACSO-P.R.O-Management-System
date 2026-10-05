using MediatR;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoicePdf;

public sealed record GetServiceFeeInvoicePdfQuery(
    Guid Id,
    Guid? ClientCompanyId = null
) : IRequest<GetServiceFeeInvoicePdfResponseDto>;
