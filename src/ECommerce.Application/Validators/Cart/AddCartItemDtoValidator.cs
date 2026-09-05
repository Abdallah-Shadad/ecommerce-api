using ECommerce.Application.DTOs.Cart;
using FluentValidation;

namespace ECommerce.Application.Validators.Cart;

public class AddCartItemDtoValidator : AbstractValidator<AddCartItemDto>
{
    public AddCartItemDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Invalid Product ID.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be at least 1.")
            .LessThanOrEqualTo(100)
            .WithMessage("Cannot add more than 100 items of a single product at once.");
    }
}