using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Helpers;

public static class DocumentStatusCalculator
{
    private const int ExpiringSoonDays = 90;

    public static DocumentStatus Calculate(
        DateTime? expiryDate,
        bool isActive,
        bool isDeleted)
    {
        // Deleted or inactive documents
        // are not considered active.
        if (isDeleted || !isActive)
        {
            return DocumentStatus.Overdue;
        }

        // No expiry date means we cannot determine
        // an expiry-based warning.
        if (!expiryDate.HasValue)
        {
            return DocumentStatus.Active;
        }

        var today = DateTime.UtcNow.Date;
        var expiryDateOnly = expiryDate.Value.Date;

        // Already expired
        if (expiryDateOnly < today)
        {
            return DocumentStatus.Overdue;
        }

        var daysRemaining =
            (expiryDateOnly - today).Days;

        // Expires within 90 days
        if (daysRemaining <= ExpiringSoonDays)
        {
            return DocumentStatus.ExpiringSoon;
        }

        // More than 90 days remaining
        return DocumentStatus.Active;
    }
}