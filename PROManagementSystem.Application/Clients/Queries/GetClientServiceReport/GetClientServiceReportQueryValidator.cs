using FluentValidation;

namespace PROManagementSystem.Application.Clients.Queries.GetClientServiceReport;

public sealed class GetClientServiceReportQueryValidator
    : AbstractValidator<GetClientServiceReportQuery>
{
    public GetClientServiceReportQueryValidator()
    {
        RuleFor(x => x.From)
            .LessThan(x => x.To)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("From must be before To.");

        RuleFor(x => x.To)
            .LessThanOrEqualTo(x => x.From!.Value.AddDays(365))
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("Date range cannot exceed 365 days.");
    }
}
