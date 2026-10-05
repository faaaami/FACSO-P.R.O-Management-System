using MediatR;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;

public sealed record GetRetainerInvoiceByIdQuery(
    Guid Id,
    Guid? ClientCompanyId = null
) : IRequest<GetRetainerInvoiceByIdResponseDto>;
