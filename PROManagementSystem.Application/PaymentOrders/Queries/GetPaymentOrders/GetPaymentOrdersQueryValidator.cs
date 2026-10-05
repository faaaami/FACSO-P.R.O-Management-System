using FluentValidation;

namespace PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;

public sealed class GetPaymentOrdersQueryValidator
    : AbstractValidator<GetPaymentOrdersQuery>
{
    public GetPaymentOrdersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}