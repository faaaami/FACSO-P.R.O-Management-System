using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Pdf;

public sealed class QuestInvoicePdfService
    : IInvoicePdfService
{
    public Task<byte[]> GenerateAsync(
        InvoicePdfModel invoice,
        CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(40);

                page.Header().Element(header =>
                {
                    header
                        .Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item()
                                    .Text("PRO Management System")
                                    .FontSize(18)
                                    .Bold()
                                    .FontColor(
                                        Colors.Blue.Medium);

                                var invoiceType =
                                    invoice.InvoiceType ==
                                        "ServiceFee"
                                        ? "Service Fee Invoice"
                                        : "Retainer Invoice";

                                col.Item()
                                    .Text(invoiceType)
                                    .FontSize(14)
                                    .FontColor(
                                        Colors.Grey.Darken1);
                            });

                            row.RelativeItem().AlignRight()
                                .Column(col =>
                                {
                                    col.Item()
                                        .Text(
                                            $"Invoice #: {invoice.InvoiceNumber}")
                                        .FontSize(11)
                                        .Bold();

                                    col.Item()
                                        .Text(
                                            $"Date: {invoice.InvoiceDate:dd MMM yyyy}")
                                        .FontSize(10);

                                    col.Item()
                                        .Text(
                                            $"Due: {invoice.DueDate:dd MMM yyyy}")
                                        .FontSize(10);
                                });
                        });
                });

                page.Content().Element(content =>
                {
                    content.Column(column =>
                    {
                        column.Item()
                            .PaddingVertical(10)
                            .LineHorizontal(1)
                            .LineColor(
                                Colors.Grey.Lighten1);

                        column.Item()
                            .PaddingTop(15)
                            .Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item()
                                        .Text("Bill To")
                                        .FontSize(11)
                                        .Bold()
                                        .FontColor(
                                            Colors.Grey.Darken2);

                                    col.Item()
                                        .PaddingTop(5)
                                        .Text(invoice.ClientCompanyName)
                                        .FontSize(10);

                                    if (!string.IsNullOrWhiteSpace(
                                            invoice.ContractNumber))
                                    {
                                        col.Item()
                                            .Text(
                                                $"Contract: {invoice.ContractNumber}")
                                            .FontSize(10);
                                    }
                                    else if (invoice.RenewalTaskId
                                                 .HasValue)
                                    {
                                        col.Item()
                                            .Text(
                                                $"Renewal Task: {invoice.RenewalTaskId}")
                                            .FontSize(10);
                                    }
                                });

                                row.RelativeItem().Column(col =>
                                {
                                    col.Item()
                                        .Text("Status")
                                        .FontSize(11)
                                        .Bold()
                                        .FontColor(
                                            Colors.Grey.Darken2);

                                    col.Item()
                                        .PaddingTop(5)
                                        .Text(invoice.Status)
                                        .FontSize(10)
                                        .Bold();
                                });
                            });

                        if (invoice.PeriodStart.HasValue &&
                            invoice.PeriodEnd.HasValue)
                        {
                            column.Item()
                                .PaddingTop(15)
                                .Text("Billing Period")
                                .FontSize(11)
                                .Bold()
                                .FontColor(
                                    Colors.Grey.Darken2);

                            column.Item()
                                .PaddingTop(5)
                                .Text(
                                    $"{invoice.PeriodStart:dd MMM yyyy} — {invoice.PeriodEnd:dd MMM yyyy}")
                                .FontSize(10);
                        }

                        column.Item()
                            .PaddingTop(20)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(
                                    columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                table.Header(header =>
                                {
                                    header.Cell()
                                        .Background(Colors.Blue.Darken1)
                                        .Padding(5)
                                        .Text("Description")
                                        .FontSize(10)
                                        .Bold()
                                        .FontColor(
                                            Colors.White);

                                    header.Cell()
                                        .Background(Colors.Blue.Darken1)
                                        .Padding(5)
                                        .AlignRight()
                                        .Text("Amount")
                                        .FontSize(10)
                                        .Bold()
                                        .FontColor(
                                            Colors.White);
                                });

                                var lineItem =
                                    invoice.InvoiceType ==
                                        "ServiceFee"
                                        ? "Service Fee"
                                        : "Retainer Fee";

                                table.Cell()
                                    .Text(lineItem)
                                    .FontSize(10);

                                table.Cell()
                                    .AlignRight()
                                    .Text(
                                        $"{invoice.Currency} {invoice.Amount:N2}")
                                    .FontSize(10);
                            });

                        if (!string.IsNullOrWhiteSpace(
                                invoice.Notes))
                        {
                            column.Item()
                                .PaddingTop(20)
                                .Text("Notes")
                                .FontSize(11)
                                .Bold()
                                .FontColor(
                                    Colors.Grey.Darken2);

                            column.Item()
                                .PaddingTop(5)
                                .Text(invoice.Notes)
                                .FontSize(10);
                        }
                    });
                });

                page.Footer().Element(footer =>
                {
                    footer
                        .AlignCenter()
                        .DefaultTextStyle(x =>
                            x.FontSize(9)
                             .FontColor(Colors.Grey.Medium))
                        .Text(txt =>
                        {
                            txt.Span("Page ");
                            txt.CurrentPageNumber();
                            txt.Span(" of ");
                            txt.TotalPages();
                        });
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);

        return Task.FromResult(stream.ToArray());
    }
}
