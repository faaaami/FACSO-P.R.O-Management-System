using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;

public sealed record GetRetainerInvoicesQuery(
    Guid? ClientCompanyId = null,
    Guid? ServiceContractId = null,
    RetainerInvoiceStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetRetainerInvoicesResponseDto>;
