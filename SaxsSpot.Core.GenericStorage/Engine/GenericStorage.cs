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

    public async Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> expression)
    {
        return await _dbContext.Entities.Where(expression).ToListAsync();
    }

    public async Task UpdateOrInsertAsync(TEntity entity)
    {
        var entityType = typeof(TEntity);
        var idProperty = entityType.GetProperty("Id");

        if (idProperty == null)
        {
            throw new InvalidOperationException($"Entity {entityType.Name} does not have an 'Id' property.");
        }

        var idValue = idProperty.GetValue(entity);

        if (idValue is null or long and 0)
        {
            // Если Id нет или он равен 0, значит новая сущность
            await _dbContext.Entities.AddAsync(entity);
        }
        else
        {
            var existingEntity = await _dbContext.Entities.FindAsync(idValue);
            if (existingEntity != null)
            {
                _dbContext.Entry(existingEntity).CurrentValues.SetValues(entity);
            }
            else
            {
                await _dbContext.Entities.AddAsync(entity);
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    public Task DeleteAsync(long[] ids)
    {
        throw new NotImplementedException();
    }
}