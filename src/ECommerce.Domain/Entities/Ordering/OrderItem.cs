using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Entities.Common;

namespace ECommerce.Domain.Entities.Ordering;

public class OrderItem : AuditableEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}