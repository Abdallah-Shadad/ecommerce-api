namespace ECommerce.Application.DTOs.Category;

public record CategoryDto(
    int Id,
    string Name,
    string? Description
);