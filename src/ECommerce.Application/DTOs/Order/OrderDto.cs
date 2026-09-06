namespace ECommerce.Application.DTOs.Order;

public record OrderDto(
    int Id,
    decimal TotalAmount,
    string Status,
    string ShippingAddress,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items
);