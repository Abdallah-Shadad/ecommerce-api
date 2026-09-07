using ECommerce.Application.DTOs.Cart;
using FluentValidation;

namespace ECommerce.Application.Validators.Cart;

public class UpdateCartItemDtoValidator : AbstractValidator<UpdateCartItemDto>
{
    public UpdateCartItemDtoValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be at least 1.")
            .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100.");
    }
}
