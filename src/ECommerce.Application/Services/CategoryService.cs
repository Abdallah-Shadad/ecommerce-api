using ECommerce.Application.DTOs.Category;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Interfaces.Services;
using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Categories
            .Query()
            .AsNoTracking()
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
            throw new NotFoundException($"Category with ID {id} was not found.");

        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto request, CancellationToken cancellationToken = default)
    {
        var nameExists = await _unitOfWork.Categories
            .Query()
            .AnyAsync(c => c.Name == request.Name, cancellationToken);

        if (nameExists)
            throw new ConflictException($"Category with name '{request.Name}' already exists.");

        var category = new Category
        {
            Name = request.Name,
            Description = request.Description
        };

        await _unitOfWork.Categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync();

        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpdateDto request, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken);

        if (category == null)
            throw new NotFoundException($"Category with ID {id} was not found.");

        var nameExists = await _unitOfWork.Categories
            .Query()
            .AnyAsync(c => c.Name == request.Name && c.Id != id, cancellationToken);

        if (nameExists)
            throw new ConflictException($"Category with name '{request.Name}' already exists.");

        category.Name = request.Name;
        category.Description = request.Description;

        _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync();

        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken);

        if (category == null)
            throw new NotFoundException($"Category with ID {id} was not found.");

        var hasAssociatedProducts = await _unitOfWork.Products
            .Query()
            .AnyAsync(p => p.CategoryId == id, cancellationToken);

        if (hasAssociatedProducts)
            throw new ConflictException($"Cannot delete category with ID {id} because it contains associated products.");

        _unitOfWork.Categories.Remove(category);
        await _unitOfWork.SaveChangesAsync();
    }
}