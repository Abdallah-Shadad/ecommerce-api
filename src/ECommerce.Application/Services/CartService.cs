using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Interfaces.Services;
using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;

    public CartService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CartDto> GetCartByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        return MapToCartDto(cart);
    }

    public async Task<CartDto> AddItemAsync(
        Guid userId,
        AddCartItemDto request,
        CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
            throw new NotFoundException($"Product with ID {request.ProductId} was not found.");

        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (existingItem != null)
        {
            var newQuantity = existingItem.Quantity + request.Quantity;

            if (newQuantity > product.StockQuantity)
            {
                throw new ConflictException(
                    $"Cannot add {request.Quantity} more items. Total requested ({newQuantity}) exceeds available stock ({product.StockQuantity}).");
            }

            existingItem.Quantity = newQuantity;
        }
        else
        {
            if (request.Quantity > product.StockQuantity)
            {
                throw new ConflictException(
                    $"Requested quantity ({request.Quantity}) exceeds available stock ({product.StockQuantity}).");
            }

            var newItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = product.Id,
                Product = product,
                Quantity = request.Quantity,
                UnitPriceSnapshot = product.Price
            };

            cart.Items.Add(newItem);
        }

        await _unitOfWork.SaveChangesAsync();
        return MapToCartDto(cart);
    }

    public async Task<CartDto> UpdateItemQuantityAsync(
        Guid userId,
        int productId,
        UpdateCartItemDto request,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
            throw new NotFoundException($"Product with ID {productId} is not present in the cart.");

        if (item.Product != null && request.Quantity > item.Product.StockQuantity)
        {
            throw new ConflictException(
                $"Requested quantity ({request.Quantity}) exceeds available stock ({item.Product.StockQuantity}).");
        }

        item.Quantity = request.Quantity;

        await _unitOfWork.SaveChangesAsync();
        return MapToCartDto(cart);
    }

    public async Task<CartDto> RemoveItemAsync(
        Guid userId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
            throw new NotFoundException($"Product with ID {productId} is not present in the cart.");

        cart.Items.Remove(item);

        await _unitOfWork.SaveChangesAsync();
        return MapToCartDto(cart);
    }

    public async Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        if (cart.Items.Count == 0)
            return;

        cart.Items.Clear();
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Cart> GetOrCreateCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _unitOfWork.Carts.Query()
            .Include(c => c.Items)
                .ThenInclude(i => i.Product!)
                    .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                Items = new List<CartItem>()
            };

            await _unitOfWork.Carts.AddAsync(cart, cancellationToken);
            await _unitOfWork.SaveChangesAsync();
        }

        return cart;
    }

    private static CartDto MapToCartDto(Cart cart)
    {
        var items = cart.Items.Select(i => new CartItemDto(
            i.ProductId,
            i.Product?.Name ?? string.Empty,
            i.Product?.Images.FirstOrDefault()?.ImageUrl,
            i.Product?.Price ?? i.UnitPriceSnapshot,
            i.UnitPriceSnapshot,
            i.Product != null && i.UnitPriceSnapshot != i.Product.Price,
            i.Quantity,
            i.Quantity * i.UnitPriceSnapshot
        )).ToList();

        var subtotal = items.Sum(i => i.TotalPrice);

        return new CartDto(cart.Id, items, subtotal);
    }
}