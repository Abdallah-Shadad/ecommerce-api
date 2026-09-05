using ECommerce.Application.Common.Models;
using ECommerce.Application.DTOs.Product;

namespace ECommerce.Application.Interfaces.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPagedProductsAsync(ProductQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(ProductCreateDto request, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateAsync(int id, ProductUpdateDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    // Uploads an image for a specific product and returns the relative path to the saved image.
    Task<string> UploadImageAsync(int productId, Stream fileStream, string originalFileName, CancellationToken cancellationToken = default);
}