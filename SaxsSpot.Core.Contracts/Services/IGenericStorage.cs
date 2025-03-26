using System.Linq.Expressions;

namespace SaxsSpot.Core.Contracts.Services;

public interface IGenericStorage<TEntity> where TEntity : class
{
    Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> expression);
    
    Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> expression);
    
    Task UpdateOrInsertAsync(TEntity nanoSystem);

    Task DeleteAsync(long[] ids);
}