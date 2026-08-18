using Microsoft.AspNetCore.Identity;
using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Entities.Ordering;

namespace ECommerce.Domain.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public Cart.Cart? Cart { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}