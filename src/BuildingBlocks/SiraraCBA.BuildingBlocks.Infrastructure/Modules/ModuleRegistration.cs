using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Modules;

/// <summary>Discovers and wires modules. The composition root's entire job.</summary>
public static class ModuleRegistration
{
    /// <summary>
    /// Finds every <see cref="IModule"/> in the given assemblies, registers it, and
    /// keeps the instances so the host can map their endpoints later.
    /// </summary>
    public static IReadOnlyList<IModule> AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        List<IModule> modules = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IModule).IsAssignableFrom(type)
                           && type is { IsAbstract: false, IsInterface: false })
            .Select(type => (IModule)Activator.CreateInstance(type)!)
            .OrderBy(module => module.Name, StringComparer.Ordinal)
            .ToList();

        List<string> duplicates = modules
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            // Two modules sharing a name means two modules sharing a schema.
            // Fail at boot rather than discovering it in a migration.
            throw new InvalidOperationException(
                $"Duplicate module names: {string.Join(", ", duplicates)}.");
        }

        foreach (IModule module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return modules;
    }

    /// <summary>Maps every discovered module's endpoints.</summary>
    public static IEndpointRouteBuilder MapModules(
        this IEndpointRouteBuilder endpoints,
        IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(modules);

        foreach (IModule module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
