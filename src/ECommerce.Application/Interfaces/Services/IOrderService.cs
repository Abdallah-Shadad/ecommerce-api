using ECommerce.Application.Common.Models;
using ECommerce.Application.DTOs.Order;

namespace ECommerce.Application.Interfaces.Services;

public interface IOrderService
{
    Task<OrderDto> CheckoutAsync(Guid userId, CreateOrderDto request, CancellationToken cancellationToken = default);
    Task<OrderDto> GetOrderByIdAsync(Guid userId, int orderId, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetUserOrdersAsync(Guid userId, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetAdminOrdersAsync(OrderQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<OrderDto> UpdateOrderStatusAsync(int orderId, OrderStatusUpdateDto request, CancellationToken cancellationToken = default);
    Task<OrderDto> CancelOrderAsync(Guid userId, int orderId, CancellationToken cancellationToken = default);
}