using ECommerce.Application.DTOs.Order;
using FluentValidation;

namespace ECommerce.Application.Validators.Order;

public class OrderQueryParametersValidator : AbstractValidator<OrderQueryParameters>
{
    public OrderQueryParametersValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid order status filter.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.ToDateUtc)
            .GreaterThanOrEqualTo(x => x.FromDateUtc!.Value).WithMessage("ToDate must be greater than or equal to FromDate.")
            .When(x => x.ToDateUtc.HasValue && x.FromDateUtc.HasValue);
    }
}
