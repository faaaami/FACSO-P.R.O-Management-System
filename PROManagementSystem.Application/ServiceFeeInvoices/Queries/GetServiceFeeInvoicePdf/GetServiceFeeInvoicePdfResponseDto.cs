namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoicePdf;

public sealed record GetServiceFeeInvoicePdfResponseDto(
    byte[] PdfBytes,
    string FileName
);
