using ECommerce.Domain.Entities.Common;

namespace ECommerce.Domain.Entities.Catalog;

public class ProductImage : AuditableEntity
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;

    // Foreign Key & Navigation Properties
    public int ProductId { get; set; }
    public Product? Product { get; set; }
}