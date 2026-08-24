using MediatR;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.VoidRetainerInvoice;

public sealed record VoidRetainerInvoiceCommand(
    Guid Id,
    string? Reason
) : IRequest<VoidRetainerInvoiceResponseDto>;
