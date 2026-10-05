using FluentValidation;

namespace PROManagementSystem.Application.Clients.Queries.GetClientServiceReportPdf;

public sealed class GetClientServiceReportPdfQueryValidator
    : AbstractValidator<GetClientServiceReportPdfQuery>
{
    public GetClientServiceReportPdfQueryValidator()
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
