namespace ECommerce.Application.DTOs.Cart;

public record CartDto(
    int Id,
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal
);