using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Exceptions;
using ECommerce.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class CartServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<Cart>> _cartRepoMock;
    private readonly Mock<IRepository<Product>> _productRepoMock;
    private readonly CartService _sut;

    public CartServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cartRepoMock = new Mock<IRepository<Cart>>();
        _productRepoMock = new Mock<IRepository<Product>>();

        _unitOfWorkMock.Setup(u => u.Carts).Returns(_cartRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Products).Returns(_productRepoMock.Object);

        _sut = new CartService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task AddItemAsync_WhenProductAvailable_AddsItemToCart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product
        {
            Id = 1,
            Name = "Wireless Mouse",
            Price = 45m,
            StockQuantity = 10,
            Images = new List<ProductImage>()
        };

        var cart = new Cart
        {
            Id = 100,
            UserId = userId,
            Items = new List<CartItem>()
        };

        var carts = new List<Cart> { cart }.AsAsyncQueryable();
        _cartRepoMock.Setup(r => r.Query()).Returns(carts);
        _productRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var request = new AddCartItemDto(1, 2);

        // Act
        var result = await _sut.AddItemAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].ProductId.Should().Be(1);
        result.Items[0].Quantity.Should().Be(2);
        result.Subtotal.Should().Be(90m);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_WhenItemExists_IncrementsQuantity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product
        {
            Id = 1,
            Name = "Wireless Mouse",
            Price = 45m,
            StockQuantity = 10,
            Images = new List<ProductImage>()
        };

        var existingItem = new CartItem
        {
            CartId = 100,
            ProductId = 1,
            Product = product,
            Quantity = 2,
            UnitPriceSnapshot = 45m
        };

        var cart = new Cart
        {
            Id = 100,
            UserId = userId,
            Items = new List<CartItem> { existingItem }
        };

        var carts = new List<Cart> { cart }.AsAsyncQueryable();
        _cartRepoMock.Setup(r => r.Query()).Returns(carts);
        _productRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var request = new AddCartItemDto(1, 3);

        // Act
        var result = await _sut.AddItemAsync(userId, request);

        // Assert
        result.Items[0].Quantity.Should().Be(5); // 2 + 3
        result.Subtotal.Should().Be(225m);       // 5 * 45
    }

    [Fact]
    public async Task AddItemAsync_WhenExceedingStock_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product
        {
            Id = 1,
            Name = "Limited Edition Item",
            Price = 200m,
            StockQuantity = 3,
            Images = new List<ProductImage>()
        };

        var cart = new Cart
        {
            Id = 100,
            UserId = userId,
            Items = new List<CartItem>()
        };

        var carts = new List<Cart> { cart }.AsAsyncQueryable();
        _cartRepoMock.Setup(r => r.Query()).Returns(carts);
        _productRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var request = new AddCartItemDto(1, 5); // 5 > 3

        // Act
        var act = () => _sut.AddItemAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*exceeds available stock*");
    }

    [Fact]
    public async Task RemoveItemAsync_WhenItemExists_RemovesFromCart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product { Id = 1, Name = "Desk Pad", Price = 25m, Images = new List<ProductImage>() };
        var cartItem = new CartItem { ProductId = 1, Product = product, Quantity = 1, UnitPriceSnapshot = 25m };

        var cart = new Cart
        {
            Id = 100,
            UserId = userId,
            Items = new List<CartItem> { cartItem }
        };

        var carts = new List<Cart> { cart }.AsAsyncQueryable();
        _cartRepoMock.Setup(r => r.Query()).Returns(carts);

        // Act
        var result = await _sut.RemoveItemAsync(userId, 1);

        // Assert
        result.Items.Should().BeEmpty();
        result.Subtotal.Should().Be(0m);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
