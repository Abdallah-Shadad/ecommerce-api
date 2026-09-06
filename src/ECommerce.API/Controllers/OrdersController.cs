using System.Security.Claims;
using ECommerce.Application.DTOs.Order;
using ECommerce.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost("checkout")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<OrderDto>> Checkout(
        [FromBody] CreateOrderDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var order = await _orderService.CheckoutAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpGet]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetUserOrders(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var orders = await _orderService.GetUserOrdersAsync(userId, cancellationToken);
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var order = await _orderService.GetOrderByIdAsync(userId, id, cancellationToken);
        return Ok(order);
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<OrderDto>> UpdateOrderStatus(
        int id,
        [FromBody] OrderStatusUpdateDto request,
        CancellationToken cancellationToken)
    {
        var updatedOrder = await _orderService.UpdateOrderStatusAsync(id, request, cancellationToken);
        return Ok(updatedOrder);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID could not be identified from token.");
        }
        return userId;
    }
}