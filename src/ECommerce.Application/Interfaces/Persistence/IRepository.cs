
namespace ECommerce.Application.Interfaces.Persistence;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IReadOnlyList<T>> ListAllAsync();
    IQueryable<T> Query();   // for composable filtering in services
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}
