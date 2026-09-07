using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Entities.Ordering;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Application.Interfaces.Persistence;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<Product> Products { get; }
    IRepository<ProductImage> ProductImages { get; }
    IRepository<Category> Categories { get; }
    IRepository<Cart> Carts { get; }
    IRepository<CartItem> CartItems { get; }
    IRepository<Order> Orders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    IExecutionStrategy CreateExecutionStrategy();
}