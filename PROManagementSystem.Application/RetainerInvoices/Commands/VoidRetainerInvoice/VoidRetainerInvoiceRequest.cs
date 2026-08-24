namespace PROManagementSystem.Application.RetainerInvoices.Commands.VoidRetainerInvoice;

public sealed record VoidRetainerInvoiceRequest(
    string? Reason
);
