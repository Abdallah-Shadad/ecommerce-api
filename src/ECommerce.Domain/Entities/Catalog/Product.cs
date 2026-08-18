using ECommerce.Domain.Entities.Common;

namespace ECommerce.Domain.Entities.Catalog;

public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string SKU { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Concurrency Token
    public byte[] RowVersion { get; set; } = [];

    // Foreign Key & Navigation Properties
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}