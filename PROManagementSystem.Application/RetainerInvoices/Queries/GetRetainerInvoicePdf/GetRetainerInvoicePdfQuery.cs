using MediatR;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoicePdf;

public sealed record GetRetainerInvoicePdfQuery(
    Guid Id,
    Guid? ClientCompanyId = null
) : IRequest<GetRetainerInvoicePdfResponseDto>;
