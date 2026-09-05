namespace ECommerce.Application.DTOs.Product;

public record ProductDto(
    int Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    int StockQuantity,
    string SKU,
    int CategoryId,
    string CategoryName,
    IReadOnlyList<string> ImageUrls
);