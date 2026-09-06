using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs.Order;

public record OrderStatusUpdateDto(
    OrderStatus Status
);