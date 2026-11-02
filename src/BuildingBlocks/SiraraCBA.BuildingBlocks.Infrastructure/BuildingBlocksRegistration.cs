using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SiraraCBA.BuildingBlocks.Application.Abstractions;
using SiraraCBA.BuildingBlocks.Application.Messaging;
using SiraraCBA.BuildingBlocks.Infrastructure.Identifiers;
using SiraraCBA.BuildingBlocks.Infrastructure.Monetary;
using SiraraCBA.BuildingBlocks.Infrastructure.Time;
using SiraraCBA.SharedKernel.Monetary;

namespace SiraraCBA.BuildingBlocks.Infrastructure;

/// <summary>Registers the primitives every module depends on.</summary>
public static class BuildingBlocksRegistration
{
    public static IServiceCollection AddBuildingBlocks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Options are validated at startup, so a misconfigured deployment fails at
        // boot instead of at 2 a.m. under load (coding-standards.md, Configuration).
        services.AddOptions<BusinessCalendarOptions>()
            .Bind(configuration.GetSection(BusinessCalendarOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CurrencyOptions>()
            .Bind(configuration.GetSection(CurrencyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, UuidV7IdGenerator>();

        services.AddSingleton<ICurrencyRegistry>(sp =>
            CurrencyRegistryFactory.Create(sp.GetRequiredService<IOptions<CurrencyOptions>>()));

        services.AddScoped<ISender, Messaging.RequestDispatcher>();

        return services;
    }
}
