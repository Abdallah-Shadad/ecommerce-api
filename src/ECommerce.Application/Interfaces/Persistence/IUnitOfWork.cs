using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Entities.Ordering;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Application.Interfaces.Persistence;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<Product> Products { get; }
    IRepository<Category> Categories { get; }
    IRepository<Cart> Carts { get; }
    IRepository<Order> Orders { get; }

    Task<int> SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    IExecutionStrategy CreateExecutionStrategy();
}