using ECommerce.Domain.Entities.Common;
using ECommerce.Domain.Entities.Identity;

namespace ECommerce.Domain.Entities.Cart;

public class Cart : AuditableEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}