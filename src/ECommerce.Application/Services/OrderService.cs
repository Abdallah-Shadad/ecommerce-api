using ECommerce.Application.DTOs.Order;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Interfaces.Services;
using ECommerce.Domain.Entities.Ordering;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;

    public OrderService(IUnitOfWork unitOfWork, IPaymentService paymentService)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
    }

    public async Task<OrderDto> CheckoutAsync(Guid userId, CreateOrderDto request, CancellationToken cancellationToken = default)
    {
        var cart = await _unitOfWork.Carts.Query()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (cart == null || !cart.Items.Any())
            throw new BadRequestException("Cart is empty or does not exist.");

        var strategy = _unitOfWork.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var orderItems = new List<OrderItem>();
                decimal totalAmount = 0;

                // retrieve each product and check stock
                foreach (var cartItem in cart.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(cartItem.ProductId, cancellationToken);
                    if (product == null)
                        throw new NotFoundException($"Product with ID {cartItem.ProductId} not found.");

                    // check stock quantity
                    if (product.StockQuantity < cartItem.Quantity)
                        throw new ConflictException($"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {cartItem.Quantity}");

                    // subtract the quantity from stock
                    product.StockQuantity -= cartItem.Quantity;

                    // create order item snapshot
                    var orderItem = new OrderItem
                    {
                        ProductId = product.Id,
                        ProductNameSnapshot = product.Name,
                        UnitPrice = product.Price,
                        Quantity = cartItem.Quantity
                    };

                    orderItems.Add(orderItem);
                    totalAmount += orderItem.UnitPrice * orderItem.Quantity;
                }

                // simulate payment processing
                var paymentResult = await _paymentService.AuthorizePaymentAsync(totalAmount, cancellationToken);
                if (!paymentResult.IsSuccess)
                    throw new BadRequestException($"Payment failed: {paymentResult.ErrorMessage}");

                // create order unique number
                var orderNumber = $"ORD-{Guid.NewGuid().ToString()[..8].ToUpper()}";

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    UserId = userId,
                    ShippingAddress = request.ShippingAddress,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.Pending,
                    Items = orderItems,
                    Payment = new Payment
                    {
                        Amount = totalAmount,
                        Status = PaymentStatus.Succeeded,
                        TransactionReference = paymentResult.TransactionReference ?? Guid.NewGuid().ToString()
                    }
                };

                await _unitOfWork.Orders.AddAsync(order, cancellationToken);
                // remove items from cart after successful order creation
                cart.Items.Clear();

                // stock quantities have already been updated in the product entities, so we just need to save changes
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync(cancellationToken);

                return MapToDto(order);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ConflictException("One or more items in your cart were modified concurrently. Please review your cart and try again.");
            }
        });
    }

    public async Task<OrderDto> GetOrderByIdAsync(Guid userId, int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);

        if (order == null)
            throw new NotFoundException($"Order with ID {orderId} was not found.");

        return MapToDto(order);
    }

    public async Task<IReadOnlyList<OrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int orderId, OrderStatusUpdateDto request, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            throw new NotFoundException($"Order with ID {orderId} was not found.");

        if (request.Status == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
        {
            foreach (var item in order.Items)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId, cancellationToken);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                }
            }
        }

        order.Status = request.Status;

        await _unitOfWork.SaveChangesAsync();

        return MapToDto(order);
    }

    private static OrderDto MapToDto(Order order)
    {
        var itemsDto = order.Items.Select(item => new OrderItemDto(
            item.ProductId,
            item.ProductNameSnapshot,
            item.UnitPrice,
            item.Quantity,
            item.UnitPrice * item.Quantity
        )).ToList();

        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.TotalAmount,
            order.Status.ToString(),
            order.ShippingAddress,
            order.CreatedAtUtc,
            itemsDto
        );
    }
}