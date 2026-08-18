using ECommerce.Domain.Entities.Common;
using ECommerce.Domain.Entities.Identity;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities.Ordering;

public class Order : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;

    // Navigation Properties
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
}