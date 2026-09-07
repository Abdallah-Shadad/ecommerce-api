using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ECommerce.Application.Common.Models;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Order;
using FluentAssertions;

namespace ECommerce.IntegrationTests;

public class UserJourneyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserJourneyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteUserJourney_Register_Login_Cart_Checkout_QueryOrder_Succeeds()
    {
        await _factory.InitializeDatabaseAsync();

        // 1. REGISTER
        var registerRequest = new RegisterRequest(
            "Tamer Hosny",
            "tamer.integration@ecommerce.com",
            "ComplexPass123!",
            "ComplexPass123!"
        );

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authData = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        authData.Should().NotBeNull();
        authData!.AccessToken.Should().NotBeNullOrWhiteSpace();

        var token = authData.AccessToken;

        // 2. LOGIN
        var loginRequest = new LoginRequest(
            "tamer.integration@ecommerce.com",
            "ComplexPass123!"
        );

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginData.Should().NotBeNull();
        loginData!.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Configure authenticated client
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 3. ADD ITEM TO CART (Product ID 1: Gaming Mouse, Price 60, Qty 2)
        var addItemRequest = new AddCartItemDto(ProductId: 1, Quantity: 2);
        var cartResponse = await _client.PostAsJsonAsync("/api/cart/items", addItemRequest);
        cartResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cartData = await cartResponse.Content.ReadFromJsonAsync<CartDto>();
        cartData.Should().NotBeNull();
        cartData!.Items.Should().HaveCount(1);
        cartData.Items[0].ProductId.Should().Be(1);
        cartData.Items[0].Quantity.Should().Be(2);
        cartData.Subtotal.Should().Be(120m);

        // 4. CHECKOUT
        var checkoutRequest = new CreateOrderDto("Villa 42, 5th Settlement, New Cairo, Egypt");
        var checkoutResponse = await _client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);
        var responseBody = await checkoutResponse.Content.ReadAsStringAsync();
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.Created, because: $"checkout response was: {responseBody}");

        var orderData = await checkoutResponse.Content.ReadFromJsonAsync<OrderDto>();
        orderData.Should().NotBeNull();
        orderData!.OrderNumber.Should().StartWith("ORD-");
        orderData.TotalAmount.Should().Be(120m);
        orderData.Status.Should().Be("Confirmed");
        orderData.Items.Should().HaveCount(1);

        // 5. QUERY ORDERS
        var queryOrdersResponse = await _client.GetAsync("/api/orders?pageNumber=1&pageSize=10");
        queryOrdersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedOrders = await queryOrdersResponse.Content.ReadFromJsonAsync<PagedResult<OrderDto>>();
        pagedOrders.Should().NotBeNull();
        pagedOrders!.Items.Should().HaveCount(1);
        pagedOrders.Items[0].OrderNumber.Should().Be(orderData.OrderNumber);
        pagedOrders.Items[0].TotalAmount.Should().Be(120m);
    }
}
