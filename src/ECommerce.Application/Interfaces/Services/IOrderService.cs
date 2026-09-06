using ECommerce.Application.DTOs.Order;

namespace ECommerce.Application.Interfaces.Services;

public interface IOrderService
{
    Task<OrderDto> CheckoutAsync(Guid userId, CreateOrderDto request, CancellationToken cancellationToken = default);

    Task<OrderDto> GetOrderByIdAsync(Guid userId, int orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<OrderDto> UpdateOrderStatusAsync(int orderId, OrderStatusUpdateDto request, CancellationToken cancellationToken = default);
}