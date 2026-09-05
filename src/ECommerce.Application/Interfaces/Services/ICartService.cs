using ECommerce.Application.DTOs.Cart;

namespace ECommerce.Application.Interfaces.Services;

public interface ICartService
{
    Task<CartDto> GetCartByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CartDto> AddItemAsync(Guid userId, AddCartItemDto request, CancellationToken cancellationToken = default);
    Task<CartDto> UpdateItemQuantityAsync(Guid userId, int productId, UpdateCartItemDto request, CancellationToken cancellationToken = default);
    Task<CartDto> RemoveItemAsync(Guid userId, int productId, CancellationToken cancellationToken = default);
    Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default);
}