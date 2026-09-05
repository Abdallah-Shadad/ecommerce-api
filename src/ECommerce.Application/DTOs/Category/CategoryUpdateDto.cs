using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Category;

public record CategoryUpdateDto(
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Category name must be between 2 and 80 characters.")]
    string Name,

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    string? Description
);