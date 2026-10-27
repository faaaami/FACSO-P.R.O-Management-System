using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Notifications;

/// <summary>
/// Plain-language names for the records notifications talk about, so a
/// message reads "Visa 784-1990 for Ahmed Ali (Acme LLC)" rather than an id.
/// </summary>
public static partial class NotificationLabels
{
    /// <summary>"Visa 784-1990 for Ahmed Ali (Acme LLC)".</summary>
    public static async Task<string> RenewalTaskAsync(
        IApplicationDbContext context,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var row = await context.RenewalTasks
            .Where(x => x.Id == taskId)
            .Select(x => new
            {
                x.Document!.Type,
                x.Document.DocumentNumber,
                Employee = x.Document.Employee != null
                    ? x.Document.Employee.FullName
                    : null,
                Company = x.ClientCompany != null
                    ? x.ClientCompany.CompanyName
                    : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return "a renewal task";
        }

        var label = $"{DocumentType(row.Type)} {row.DocumentNumber}".Trim();

        if (!string.IsNullOrWhiteSpace(row.Employee))
        {
            label += $" for {row.Employee}";
        }

        if (!string.IsNullOrWhiteSpace(row.Company))
        {
            label += $" ({row.Company})";
        }

        return label;
    }

    /// <summary>"Emirates ID", "Trade license", ...</summary>
    public static string DocumentType(DocumentType type) =>
        type == Domain.Enums.DocumentType.EmiratesId
            ? "Emirates ID"
            : Humanize(type.ToString());

    /// <summary>"AED 1,250.00".</summary>
    public static string Money(decimal amount, string currency) =>
        $"{currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    /// <summary>Client message for a newly issued invoice.</summary>
    public static string NewInvoice(
        string invoiceNumber,
        decimal amount,
        string currency,
        DateOnly dueDate) =>
        $"Invoice {invoiceNumber} for {Money(amount, currency)}, due " +
        $"{dueDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}, is " +
        "ready. You can view and pay it under Billing.";

    private static string Humanize(string name)
    {
        var words = SplitWords().Replace(name, " $1").Trim();

        return words.Length == 0
            ? words
            : words[0] + words[1..].ToLowerInvariant();
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex SplitWords();
}
