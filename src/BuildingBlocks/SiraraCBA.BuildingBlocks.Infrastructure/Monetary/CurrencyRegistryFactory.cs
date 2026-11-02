using Microsoft.Extensions.Options;
using SiraraCBA.SharedKernel.Monetary;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Monetary;

/// <summary>Builds the currency registry from configuration, failing at boot if it is wrong.</summary>
internal static class CurrencyRegistryFactory
{
    public static ICurrencyRegistry Create(IOptions<CurrencyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<Currency> currencies = [];
        List<string> problems = [];

        foreach (CurrencyOptions.CurrencyDefinition definition in options.Value.Supported)
        {
            Result<Currency> created = Currency.Create(definition.Code, definition.MinorUnits);

            if (created.IsFailure)
            {
                problems.Add(created.Error.ToString());
                continue;
            }

            currencies.Add(created.Value);
        }

        if (problems.Count > 0)
        {
            throw new OptionsValidationException(
                CurrencyOptions.SectionName,
                typeof(CurrencyOptions),
                problems);
        }

        return new InMemoryCurrencyRegistry(currencies);
    }
}
