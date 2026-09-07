using ECommerce.Application.DTOs.Product;
using FluentValidation;

namespace ECommerce.Application.Validators.Product;

public class ProductQueryParametersValidator : AbstractValidator<ProductQueryParameters>
{
    private static readonly string[] ValidSortColumns = ["name", "price", "newest"];

    public ProductQueryParametersValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Min price cannot be negative.")
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Max price cannot be negative.")
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value).WithMessage("Max price must be greater than or equal to min price.")
            .When(x => x.MaxPrice.HasValue && x.MinPrice.HasValue);

        RuleFor(x => x.SortBy)
            .Must(x => string.IsNullOrEmpty(x) || ValidSortColumns.Contains(x.ToLowerInvariant()))
            .WithMessage($"SortBy must be one of: {string.Join(", ", ValidSortColumns)}.")
            .When(x => !string.IsNullOrEmpty(x.SortBy));
    }
}
