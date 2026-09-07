using System.Text.RegularExpressions;
using ECommerce.Application.Common.Models;
using ECommerce.Application.DTOs.Product;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Interfaces.Services;
using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;

    public ProductService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
    {
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
    }

    public async Task<PagedResult<ProductDto>> GetPagedProductsAsync(
        ProductQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Products.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) ||
                                     p.Description.ToLower().Contains(term));
        }

        if (parameters.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == parameters.CategoryId.Value);
        }

        if (parameters.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= parameters.MinPrice.Value);
        }

        if (parameters.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= parameters.MaxPrice.Value);
        }

        if (parameters.InStockOnly.HasValue && parameters.InStockOnly.Value)
        {
            query = query.Where(p => p.StockQuantity > 0);
        }

        query = (parameters.SortBy?.ToLower(), parameters.Descending) switch
        {
            ("price", false) => query.OrderBy(p => p.Price),
            ("price", true) => query.OrderByDescending(p => p.Price),
            ("newest", _) => query.OrderByDescending(p => p.CreatedAtUtc),
            ("name", true) => query.OrderByDescending(p => p.Name),
            _ => query.OrderBy(p => p.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.SKU,
                p.CategoryId,
                p.Category != null ? p.Category.Name : string.Empty,
                p.Images.Select(img => img.ImageUrl).ToList()
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.Query()
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.SKU,
                p.CategoryId,
                p.Category != null ? p.Category.Name : string.Empty,
                p.Images.Select(img => img.ImageUrl).ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (product == null)
            throw new NotFoundException($"Product with ID {id} was not found.");

        return product;
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto request, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category == null)
            throw new NotFoundException($"Category with ID {request.CategoryId} was not found.");

        var skuExists = await _unitOfWork.Products.Query()
            .AnyAsync(p => p.SKU == request.SKU, cancellationToken);

        if (skuExists)
            throw new ConflictException($"A product with SKU '{request.SKU}' already exists.");

        var product = new Product
        {
            Name = request.Name,
            Slug = GenerateSlug(request.Name),
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            SKU = request.SKU,
            CategoryId = request.CategoryId
        };

        await _unitOfWork.Products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync();

        return new ProductDto(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.SKU,
            product.CategoryId,
            category.Name,
            Array.Empty<string>()
        );
    }

    public async Task<ProductDto> UpdateAsync(int id, ProductUpdateDto request, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product == null)
            throw new NotFoundException($"Product with ID {id} was not found.");

        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category == null)
            throw new NotFoundException($"Category with ID {request.CategoryId} was not found.");

        var skuExists = await _unitOfWork.Products.Query()
            .AnyAsync(p => p.SKU == request.SKU && p.Id != id, cancellationToken);

        if (skuExists)
            throw new ConflictException($"A product with SKU '{request.SKU}' already exists.");

        product.Name = request.Name;
        product.Slug = GenerateSlug(request.Name);
        product.Description = request.Description;
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.SKU = request.SKU;
        product.CategoryId = request.CategoryId;

        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        var imageUrls = await _unitOfWork.Products.Query()
            .Where(p => p.Id == id)
            .SelectMany(p => p.Images.Select(i => i.ImageUrl))
            .ToListAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.SKU,
            product.CategoryId,
            category.Name,
            imageUrls
        );
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product == null)
            throw new NotFoundException($"Product with ID {id} was not found.");

        _unitOfWork.Products.Remove(product);
        await _unitOfWork.SaveChangesAsync();
    }


    public async Task<string> UploadImageAsync(
    int productId,
    Stream fileStream,
    string originalFileName,
    CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId, cancellationToken);
        if (product == null)
            throw new NotFoundException($"Product with ID {productId} was not found.");

        // save the file using the file storage service
        var imageUrl = await _fileStorageService.SaveFileAsync(
            fileStream,
            originalFileName,
            "products",
            cancellationToken);

        // create a new ProductImage entity and associate it with the product
        var productImage = new ProductImage
        {
            ProductId = productId,
            ImageUrl = imageUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        // add the new image to the product's Images collection and save changes
        product.Images.Add(productImage);
        await _unitOfWork.SaveChangesAsync();

        return imageUrl;
    }


    private static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", " ").Trim();
        slug = Regex.Replace(slug, @"\s", "-");
        return slug;
    }
}