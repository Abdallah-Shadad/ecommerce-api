using ECommerce.Application.DTOs.Order;
using FluentValidation;

namespace ECommerce.Application.Validators.Order;

public class OrderStatusUpdateDtoValidator : AbstractValidator<OrderStatusUpdateDto>
{
    public OrderStatusUpdateDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid order status value.");
    }
}
