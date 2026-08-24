using PROManagementSystem.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PROManagementSystem.Infrastructure.Pdf;

public sealed class QuestReportPdfService
    : IReportPdfService
{
    public byte[] GenerateClientServiceReport(
        ClientServiceReportDto report)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Text($"Service Report — {report.Client.CompanyName}")
                    .FontSize(18).Bold().FontColor(Colors.Blue.Medium);
                page.Content().Column(column =>
                {
                    column.Item().PaddingTop(10).Text("Summary")
                        .FontSize(14).Bold();
                    column.Item().PaddingTop(5).Text(
                        $"Total Tasks: {report.Summary.TotalTasks}  |  " +
                        $"Completed: {report.Summary.CompletedTasks}  |  " +
                        $"Pending: {report.Summary.PendingTasks}");
                    column.Item().Text(
                        $"Total Invoiced: {report.Summary.TotalInvoiced:N2}  |  " +
                        $"Total Paid: {report.Summary.TotalPaid:N2}  |  " +
                        $"Invoices: {report.Summary.TotalInvoices}");

                    if (report.Tasks.Any())
                    {
                        column.Item().PaddingTop(15).Text("Tasks")
                            .FontSize(14).Bold();
                        column.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Text("Document").Bold();
                                header.Cell().Text("Status").Bold();
                                header.Cell().Text("Completed").Bold();
                                header.Cell().Text("Staff").Bold();
                            });
                            foreach (var task in report.Tasks)
                            {
                                table.Cell().Text(task.DocumentNumber ?? "N/A");
                                table.Cell().Text(task.Status);
                                table.Cell().Text(task.CompletedAt?.ToString("dd MMM yyyy") ?? "-");
                                table.Cell().Text(task.AssignedStaffName ?? "-");
                            }
                        });
                    }

                    if (report.Invoices.Any())
                    {
                        column.Item().PaddingTop(15).Text("Invoices")
                            .FontSize(14).Bold();
                        column.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Text("Number").Bold();
                                header.Cell().Text("Type").Bold();
                                header.Cell().Text("Amount").Bold();
                                header.Cell().Text("Status").Bold();
                                header.Cell().Text("Date").Bold();
                            });
                            foreach (var inv in report.Invoices)
                            {
                                table.Cell().Text(inv.InvoiceNumber);
                                table.Cell().Text(inv.Type);
                                table.Cell().Text($"{inv.Amount:N2}");
                                table.Cell().Text(inv.Status);
                                table.Cell().Text(inv.CreatedAt.ToString("dd MMM yyyy"));
                            }
                        });
                    }
                });
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated on ");
                    x.CurrentPageNumber();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }
}
