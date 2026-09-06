using ECommerce.Application.DTOs.Order;
using FluentValidation;

namespace ECommerce.Application.Validators.Order;

public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.ShippingAddress)
            .NotEmpty()
            .WithMessage("Shipping address is required.")
            .MinimumLength(10)
            .WithMessage("Shipping address must be at least 10 characters.")
            .MaximumLength(500)
            .WithMessage("Shipping address cannot exceed 500 characters.");
    }
}