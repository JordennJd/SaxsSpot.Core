using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SaxsSpot.Core.Contracts.Services;

namespace SaxsSpot.Core.GenericStorage.Engine;

public abstract class GenericStorage<TEntity> : IGenericStorage<TEntity> where TEntity : class
{
    private readonly GenericDbContext<TEntity> _dbContext;
    
    protected GenericStorage(GenericDbContext<TEntity> dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> expression)
    {
        return await _dbContext.Entities.FirstOrDefaultAsync(expression);
    }

    public Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> expression)
    {
        throw new NotImplementedException();
    }

    public Task UpdateOrInsertAsync(TEntity nanoSystem)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(long[] ids)
    {
        throw new NotImplementedException();
    }
}