namespace ECommerce.Application.DTOs.Order;

public record OrderDto(
    int Id,
    string OrderNumber,
    decimal TotalAmount,
    string Status,
    string ShippingAddress,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items
);