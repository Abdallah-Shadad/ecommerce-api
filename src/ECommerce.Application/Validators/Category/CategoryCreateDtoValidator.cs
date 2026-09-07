using ECommerce.Application.DTOs.Category;
using FluentValidation;

namespace ECommerce.Application.Validators.Category;

public class CategoryCreateDtoValidator : AbstractValidator<CategoryCreateDto>
{
    public CategoryCreateDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(80).WithMessage("Category name cannot exceed 80 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Category description cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
