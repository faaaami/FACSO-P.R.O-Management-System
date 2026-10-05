using MediatR;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;

public sealed record GetServiceFeeInvoiceByIdQuery(
    Guid Id,
    Guid? ClientCompanyId = null
) : IRequest<GetServiceFeeInvoiceByIdResponseDto>;
