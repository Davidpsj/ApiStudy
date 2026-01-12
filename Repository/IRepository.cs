using System.Linq.Expressions;

namespace ApiStudy.Repository;

public interface IRepository<T> where T : class
{
    public bool UsingTransaction { get; set; }
    public Task<List<T>> GetAllAsync();
    public Task<List<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);
    public Task<T?> GetByIdAsync(Guid id);
    public Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes);
    public Task<IQueryable<T>> GetByQuery(Expression<Func<T, bool>> predicate);
    public Task<IQueryable<T>> GetByQuery(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);
    public Task<T> CreateAsync(T entity);
    public Task<T?> UpdateAsync(Guid id, T entity);
    public Task<bool> DeleteAsync(Guid id);
    public Task<int> SaveChangesAsync();
}
