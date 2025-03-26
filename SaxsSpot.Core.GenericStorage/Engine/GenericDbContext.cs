using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SaxsSpot.Core.GenericStorage.Engine;

public class GenericDbContext<TEntity> : DbContext where TEntity : class
{
    private readonly IConfiguration _configuration;
    
    public GenericDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public DbSet<TEntity> Entities { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = _configuration.GetConnectionString("psql");
        if (connectionString is not null)
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            base.OnConfiguring(optionsBuilder);
        }
    }
}