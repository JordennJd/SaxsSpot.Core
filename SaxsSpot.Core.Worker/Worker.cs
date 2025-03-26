using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaxsSpot.Core.Contracts.Attributes;
using SaxsSpot.Core.Contracts.Services;
using SaxsSpot.Core.GenericStorage.Engine;
using Serilog;
using SaxsSpot.Transport.Receiver.Engine.Services;

namespace SaxsSpot.Core.Worker;

public class Worker
{
    private IConfiguration _configuration;
    
    public async Task Start()
    {
        var builder = Host.CreateApplicationBuilder();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File($"{Directory.GetCurrentDirectory()}/logs/main.log", rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();

        try
        {
            builder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.dev.json", optional: false, reloadOnChange: true);
            
            _configuration = builder.Configuration;
        }
        catch (Exception e)
        {
            Log.Logger.Error(e.Message);
            throw;
        }
        
        builder.Services.AddHostedService<MessageReceiver>();
        
        foreach (var dll in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "SaxsSpot.*.dll"))
        {
            try
            {
                Assembly.LoadFrom(dll);
            }
            catch {}
        }

        var saxsAssemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(x => x.FullName.StartsWith("SaxsSpot"))
            .ToList();
        
        RegisterServices(saxsAssemblies, builder.Services);
        RegisterStorages(saxsAssemblies, builder.Services);
        
        await builder.Build().RunAsync();
    }

    private void RegisterServices(IEnumerable<Assembly> saxsAssemblies, IServiceCollection serviceCollection)
    {
        var allTypes = saxsAssemblies.SelectMany(x =>
            x.GetTypes())
            .ToList();
        
        var saxsServices = allTypes.Where(x => x.GetCustomAttribute(typeof(SaxsServiceAttribute)) is not null);

        foreach (var saxsService in saxsServices)
        {
            Log.Logger.Information($"{saxsService.FullName} try to register at DI container");

            try
            {
                if (saxsService.IsInterface)
                {
                    var implementation = allTypes.SingleOrDefault(x => x.IsClass && saxsService.Name == 'I' + x.Name
                        && x.IsAssignableTo(saxsService));

                    if (implementation is not null)
                    {
                        serviceCollection.AddScoped(saxsService, implementation);
                    }
                    else
                    {
                        Log.Logger.Information($"implementation not found for {saxsService.FullName}");
                        continue;
                    }
                }
                
                Log.Logger.Information($"{saxsService.FullName} successfully registered");
            }
            catch (Exception e)
            {
                Log.Logger.Warning($"{saxsService.FullName} not register in DI with error: {e.Message}");
                throw;
            }
        }
    }

    private void RegisterStorages(IEnumerable<Assembly> saxsAssemblies, IServiceCollection serviceCollection)
    {
        var baseStorageInterface = typeof(IGenericStorage<>);
        var baseDbContext = typeof(GenericDbContext<>);

        var storages = saxsAssemblies
            .SelectMany(x => x.GetTypes()
                .Where(type => type != baseStorageInterface && type.IsInterface &&
                               type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == baseStorageInterface)))
            .ToList();

        foreach (var storage in storages)
        {
            try{
                var implementation = saxsAssemblies
                    .SelectMany(x => x.GetTypes()
                        .Where(x => x is { IsAbstract: false, IsClass: true } 
                                    && x.GetInterfaces()
                                        .Any(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == baseStorageInterface)))
                    .SingleOrDefault();

                if (implementation is null)
                {
                    Log.Logger.Information($"implementation not found for {storage.FullName}");
                    continue;
                }
                
                var entityType = implementation
                    .GetInterfaces()
                    .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == baseStorageInterface)?
                    .GetGenericArguments()
                    .FirstOrDefault();
                
                if (entityType is null)
                {
                    Log.Logger.Information($"Not found generic argument for {implementation.FullName}");
                    continue;
                }
                
                var dbContext = saxsAssemblies.SelectMany(x => x.GetTypes()
                        .Where(x => x.IsSubclassOf(baseDbContext.MakeGenericType(entityType))))
                    .FirstOrDefault();
                
                if (dbContext is null)
                {
                    Log.Logger.Information($"Not found DbContext for {implementation.FullName}");
                    continue;
                }

                serviceCollection.AddScoped(dbContext);
                serviceCollection.AddScoped(storage, implementation);
            }
            catch (Exception e)
            {
                Log.Logger.Warning($"{storage.FullName} not register in DI with error: {e.Message}");
                throw;
            }
        }
    }
}