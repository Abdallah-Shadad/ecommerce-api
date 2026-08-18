using ECommerce.Domain.Entities.Common;

namespace ECommerce.Domain.Entities.Catalog;

public class Category : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation Properties
    public ICollection<Product> Products { get; set; } = new List<Product>();
}