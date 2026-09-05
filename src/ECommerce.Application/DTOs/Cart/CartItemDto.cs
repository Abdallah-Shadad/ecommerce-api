namespace ECommerce.Application.DTOs.Cart;

public record CartItemDto(
    int ProductId,
    string ProductName,
    string? ProductImageUrl,
    decimal CurrentPrice,
    decimal UnitPriceSnapshot,
    bool HasPriceChanged,
    int Quantity,
    decimal TotalPrice
);