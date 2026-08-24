using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;

public sealed record GetServiceFeeInvoicesQuery(
    Guid? ClientCompanyId = null,
    Guid? RenewalTaskId = null,
    ServiceFeeInvoiceStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetServiceFeeInvoicesResponseDto>;
