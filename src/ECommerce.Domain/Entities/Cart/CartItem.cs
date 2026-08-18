using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Entities.Common;

namespace ECommerce.Domain.Entities.Cart;

public class CartItem : AuditableEntity
{
    public int CartId { get; set; }
    public Cart? Cart { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
}