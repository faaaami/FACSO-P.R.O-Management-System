namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoicePdf;

public sealed record GetRetainerInvoicePdfResponseDto(
    byte[] PdfBytes,
    string FileName
);
