namespace ECommerce.Application.DTOs.Product;

public record ProductImageDto(
    int Id,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder,
    int ProductId
);
