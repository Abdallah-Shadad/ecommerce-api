using ECommerce.Application.Common.Models;
using ECommerce.Application.DTOs.Order;
using ECommerce.Application.Interfaces.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<OrderQueryParameters> _queryValidator;
    private readonly IValidator<OrderStatusUpdateDto> _statusValidator;

    public AdminOrdersController(
        IOrderService orderService,
        IValidator<OrderQueryParameters> queryValidator,
        IValidator<OrderStatusUpdateDto> statusValidator)
    {
        _orderService = orderService;
        _queryValidator = queryValidator;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetAdminOrders(
        [FromQuery] OrderQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var validationResult = await _queryValidator.ValidateAsync(parameters, cancellationToken);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var result = await _orderService.GetAdminOrdersAsync(parameters, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> UpdateOrderStatus(
        int id,
        [FromBody] OrderStatusUpdateDto request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _statusValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var updatedOrder = await _orderService.UpdateOrderStatusAsync(id, request, cancellationToken);
        return Ok(updatedOrder);
    }
}
