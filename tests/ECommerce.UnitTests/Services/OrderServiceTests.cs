using AutoMapper;
using ECommerce.Application.DTOs.Order;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Interfaces.Services;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Entities.Ordering;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using ECommerce.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace ECommerce.UnitTests.Services;

public class TestExecutionStrategy : IExecutionStrategy
{
    public bool RetriesOnFailure => false;

    public TResult Execute<TState, TResult>(
        TState state,
        Func<DbContext, TState, TResult> operation,
        Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded) =>
        operation(null!, state);

    public Task<TResult> ExecuteAsync<TState, TResult>(
        TState state,
        Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
        Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
        CancellationToken cancellationToken = default) =>
        operation(null!, state, cancellationToken);
}

public class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly Mock<IRepository<Cart>> _cartRepoMock;
    private readonly Mock<IRepository<CartItem>> _cartItemRepoMock;
    private readonly Mock<IRepository<Product>> _productRepoMock;
    private readonly Mock<IRepository<Order>> _orderRepoMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _cartRepoMock = new Mock<IRepository<Cart>>();
        _cartItemRepoMock = new Mock<IRepository<CartItem>>();
        _productRepoMock = new Mock<IRepository<Product>>();
        _orderRepoMock = new Mock<IRepository<Order>>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.Setup(u => u.Carts).Returns(_cartRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.CartItems).Returns(_cartItemRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Products).Returns(_productRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Orders).Returns(_orderRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.CreateExecutionStrategy()).Returns(new TestExecutionStrategy());

        // Setup Transaction mock
        var transactionMock = new Mock<IDbContextTransaction>();
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionMock.Object);

        _sut = new OrderService(_unitOfWorkMock.Object, _paymentServiceMock.Object, _mapperMock.Object);
    }

    [Fact]
    public async Task CheckoutAsync_WhenCartIsEmpty_ThrowsBadRequestException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var emptyCart = new Cart { UserId = userId, Items = new List<CartItem>() };
        var carts = new List<Cart> { emptyCart }.AsAsyncQueryable();

        _cartRepoMock.Setup(r => r.Query()).Returns(carts);

        var request = new CreateOrderDto("123 Main St, Cairo, Egypt");

        // Act
        var act = () => _sut.CheckoutAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*cart is empty*");
    }

    [Fact]
    public async Task CheckoutAsync_WhenStockIsInsufficient_ThrowsInsufficientStockException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product
        {
            Id = 1,
            Name = "Mechanical Keyboard",
            Price = 150m,
            StockQuantity = 2
        };

        var cart = new Cart
        {
            UserId = userId,
            Items = new List<CartItem>
            {
                new() { ProductId = 1, Quantity = 5, UnitPriceSnapshot = 150m }
            }
        };

        var carts = new List<Cart> { cart }.AsAsyncQueryable();
        _cartRepoMock.Setup(r => r.Query()).Returns(carts);
        _productRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var request = new CreateOrderDto("123 Main St, Cairo, Egypt");

        // Act
        var act = () => _sut.CheckoutAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<InsufficientStockException>()
            .WithMessage("*Insufficient stock*");
    }

    [Fact]
    public async Task CheckoutAsync_WhenValidStockAndPayment_CreatesOrderAndDeductsStock()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product
        {
            Id = 1,
            Name = "Mechanical Keyboard",
            Price = 100m,
            StockQuantity = 10
        };

        var cart = new Cart
        {
            UserId = userId,
            Items = new List<CartItem>
            {
                new() { ProductId = 1, Quantity = 3, UnitPriceSnapshot = 100m }
            }
        };

        var carts = new List<Cart> { cart }.AsAsyncQueryable();
        _cartRepoMock.Setup(r => r.Query()).Returns(carts);
        _productRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        _paymentServiceMock.Setup(p => p.AuthorizePaymentAsync(300m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "TX-MOCK-12345", null));

        var request = new CreateOrderDto("123 Main St, Cairo, Egypt");

        // Act
        var result = await _sut.CheckoutAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.OrderNumber.Should().StartWith("ORD-");
        result.TotalAmount.Should().Be(300m);
        result.Status.Should().Be(OrderStatus.Confirmed.ToString());
        result.Items.Should().HaveCount(1);
        result.Items[0].Quantity.Should().Be(3);
        result.Items[0].TotalPrice.Should().Be(300m);

        product.StockQuantity.Should().Be(7); // 10 - 3
        cart.Items.Should().BeEmpty();        // Cart cleared after checkout

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WhenTransitionIsValid_UpdatesStatus()
    {
        // Arrange
        var order = new Order
        {
            Id = 10,
            Status = OrderStatus.Confirmed,
            Items = new List<OrderItem>()
        };

        var orders = new List<Order> { order }.AsAsyncQueryable();
        _orderRepoMock.Setup(r => r.Query()).Returns(orders);

        var request = new OrderStatusUpdateDto(OrderStatus.Processing);

        // Act
        var result = await _sut.UpdateOrderStatusAsync(10, request);

        // Assert
        result.Status.Should().Be(OrderStatus.Processing.ToString());
        order.Status.Should().Be(OrderStatus.Processing);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WhenTransitionIsInvalid_ThrowsBadRequestException()
    {
        // Arrange
        var order = new Order
        {
            Id = 10,
            Status = OrderStatus.Pending,
            Items = new List<OrderItem>()
        };

        var orders = new List<Order> { order }.AsAsyncQueryable();
        _orderRepoMock.Setup(r => r.Query()).Returns(orders);

        var request = new OrderStatusUpdateDto(OrderStatus.Shipped); // Cannot skip Confirmed/Processing

        // Act
        var act = () => _sut.UpdateOrderStatusAsync(10, request);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Invalid order status transition*");
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsConfirmed_RestoresStockAndSetsCancelled()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product { Id = 1, StockQuantity = 5 };

        var order = new Order
        {
            Id = 20,
            UserId = userId,
            Status = OrderStatus.Confirmed,
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 4, UnitPrice = 50m }
            }
        };

        var orders = new List<Order> { order }.AsAsyncQueryable();
        _orderRepoMock.Setup(r => r.Query()).Returns(orders);
        _productRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        var result = await _sut.CancelOrderAsync(userId, 20);

        // Assert
        result.Status.Should().Be(OrderStatus.Cancelled.ToString());
        order.Status.Should().Be(OrderStatus.Cancelled);
        product.StockQuantity.Should().Be(9); // 5 + 4 restored

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsShipped_ThrowsBadRequestException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = new Order
        {
            Id = 20,
            UserId = userId,
            Status = OrderStatus.Shipped,
            Items = new List<OrderItem>()
        };

        var orders = new List<Order> { order }.AsAsyncQueryable();
        _orderRepoMock.Setup(r => r.Query()).Returns(orders);

        // Act
        var act = () => _sut.CancelOrderAsync(userId, 20);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*cannot be cancelled*");
    }

    [Fact]
    public async Task GetOrderByIdAsync_WhenOrderBelongsToAnotherUser_ThrowsNotFoundException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var order = new Order
        {
            Id = 30,
            UserId = ownerId,
            Items = new List<OrderItem>()
        };

        var orders = new List<Order> { order }.AsAsyncQueryable();
        _orderRepoMock.Setup(r => r.Query()).Returns(orders);

        // Act
        var act = () => _sut.GetOrderByIdAsync(attackerId, 30);

        // Assert (IDOR protection: 404 Not Found)
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*was not found*");
    }
}
