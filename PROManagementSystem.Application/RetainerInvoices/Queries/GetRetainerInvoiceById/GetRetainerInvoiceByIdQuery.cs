using MediatR;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;

public sealed record GetRetainerInvoiceByIdQuery(
    Guid Id
) : IRequest<GetRetainerInvoiceByIdResponseDto>;
