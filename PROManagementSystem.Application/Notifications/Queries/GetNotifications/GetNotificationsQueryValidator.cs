using FluentValidation;

namespace PROManagementSystem.Application.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryValidator
    : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortDir)
            .Must(x =>
                string.IsNullOrWhiteSpace(x) ||
                x.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Sort direction must be 'asc' or 'desc'.");

        RuleFor(x => x.SortBy)
            .Must(x =>
                string.IsNullOrWhiteSpace(x) ||
                x.Equals("createdAt", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("isRead", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Invalid sort field.");
    }
}