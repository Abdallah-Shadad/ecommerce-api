using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs.Order;

public class OrderQueryParameters
{
    public OrderStatus? Status { get; set; }
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
