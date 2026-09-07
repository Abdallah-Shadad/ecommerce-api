using AutoMapper;
using ECommerce.Application.Common.Models;
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
    private readonly IMapper _mapper;

    public OrderService(
        IUnitOfWork unitOfWork,
        IPaymentService paymentService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _mapper = mapper;
    }

    public async Task<OrderDto> CheckoutAsync(Guid userId, CreateOrderDto request, CancellationToken cancellationToken = default)
    {
        var cart = await _unitOfWork.Carts.Query()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null || !cart.Items.Any())
            throw new BadRequestException("Your shopping cart is empty.");

        var strategy = _unitOfWork.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var orderItems = new List<OrderItem>();
                decimal totalAmount = 0;

                // 1. Validate stock availability and deduct atomically
                foreach (var cartItem in cart.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(cartItem.ProductId, cancellationToken);
                    if (product == null)
                        throw new NotFoundException($"Product with ID {cartItem.ProductId} was not found.");

                    if (product.StockQuantity < cartItem.Quantity)
                    {
                        throw new InsufficientStockException(
                            $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {cartItem.Quantity}");
                    }

                    // Deduct stock (tracked entity with RowVersion optimistic concurrency)
                    product.StockQuantity -= cartItem.Quantity;

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

                // 2. Authorize Payment via Payment Gateway Abstraction
                var paymentResult = await _paymentService.AuthorizePaymentAsync(totalAmount, cancellationToken);
                if (!paymentResult.IsSuccess)
                    throw new BadRequestException($"Payment authorization failed: {paymentResult.ErrorMessage}");

                // 3. Create Order & Associated Payment Record
                var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    UserId = userId,
                    ShippingAddress = request.ShippingAddress,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.Confirmed, // Payment successfully authorized
                    Items = orderItems,
                    Payment = new Payment
                    {
                        Amount = totalAmount,
                        Status = PaymentStatus.Succeeded,
                        Provider = "Mock",
                        TransactionReference = paymentResult.TransactionReference ?? Guid.NewGuid().ToString(),
                        PaidAtUtc = DateTime.UtcNow
                    }
                };

                await _unitOfWork.Orders.AddAsync(order, cancellationToken);

                // 4. Clear shopping cart
                cart.Items.Clear();

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return MapToDto(order);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ConflictException("One or more items in your cart were modified concurrently. Please review your cart and retry.");
            }
        });
    }

    public async Task<OrderDto> GetOrderByIdAsync(Guid userId, int orderId, CancellationToken cancellationToken = default)
    {
        // Enforce IDOR protection: return 404 (not 403) to prevent ID enumeration
        var order = await _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);

        if (order == null)
            throw new NotFoundException($"Order with ID {orderId} was not found.");

        return MapToDto(order);
    }

    public async Task<PagedResult<OrderDto>> GetUserOrdersAsync(
        Guid userId,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(o => o.Items)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(MapToDto).ToList();
        return new PagedResult<OrderDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<OrderDto>> GetAdminOrdersAsync(
        OrderQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Orders.Query().AsNoTracking();

        if (parameters.Status.HasValue)
        {
            query = query.Where(o => o.Status == parameters.Status.Value);
        }

        if (parameters.FromDateUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc >= parameters.FromDateUtc.Value);
        }

        if (parameters.ToDateUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc <= parameters.ToDateUtc.Value);
        }

        query = query.OrderByDescending(o => o.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(o => o.Items)
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(MapToDto).ToList();
        return new PagedResult<OrderDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(
        int orderId,
        OrderStatusUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            throw new NotFoundException($"Order with ID {orderId} was not found.");

        if (order.Status == request.Status)
            return MapToDto(order);

        // Enforce State Machine Transitions per SRS §4.5
        ValidateStatusTransition(order.Status, request.Status);

        if (request.Status == OrderStatus.Cancelled)
        {
            var strategy = _unitOfWork.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

                // Restore stock quantities
                foreach (var item in order.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId, cancellationToken);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                order.Status = OrderStatus.Cancelled;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return MapToDto(order);
            });
        }

        order.Status = request.Status;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(order);
    }

    public async Task<OrderDto> CancelOrderAsync(
        Guid userId,
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);

        if (order == null)
            throw new NotFoundException($"Order with ID {orderId} was not found.");

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
        {
            throw new BadRequestException(
                $"Orders in '{order.Status}' status cannot be cancelled. Only Pending or Confirmed orders can be cancelled.");
        }

        var strategy = _unitOfWork.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Restore product stock inside transaction
            foreach (var item in order.Items)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId, cancellationToken);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                }
            }

            order.Status = OrderStatus.Cancelled;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return MapToDto(order);
        });
    }

    private static void ValidateStatusTransition(OrderStatus current, OrderStatus requested)
    {
        var isValid = (current, requested) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new BadRequestException(
                $"Invalid order status transition from '{current}' to '{requested}'. Allowed lifecycle: Pending -> Confirmed -> Processing -> Shipped -> Delivered (or Cancelled from Pending/Confirmed).");
        }
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