using ECommerce.Domain.Entities.Common;

namespace ECommerce.Domain.Entities.Identity;

public class RefreshToken : AuditableEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }

    // Foreign Key & Navigation
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Computed property (not mapped in DB)
    public bool IsActive => RevokedAtUtc == null && ExpiresAtUtc > DateTime.UtcNow;
}