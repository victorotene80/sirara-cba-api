namespace SiraraCBA.SharedKernel.Monetary;

/// <summary>
/// A registry over a supplied set of currency definitions.
/// </summary>
/// <remarks>
/// The definitions are injected rather than compiled in: the deployment decides
/// which currencies exist, and that list is data (configuration or a seeded table),
/// not code.
/// </remarks>
public sealed class InMemoryCurrencyRegistry : ICurrencyRegistry
{
    private readonly Dictionary<string, Currency> _byCode;

    public InMemoryCurrencyRegistry(IEnumerable<Currency> currencies)
    {
        ArgumentNullException.ThrowIfNull(currencies);

        _byCode = currencies.ToDictionary(c => c.Code, StringComparer.Ordinal);

        if (_byCode.Count == 0)
        {
            // Booting with no currencies configured means every posting will fail
            // later, far from the cause. Fail at startup instead
            // (coding-standards.md, Configuration).
            throw new InvalidOperationException("No currencies are configured.");
        }
    }

    public IReadOnlyCollection<Currency> All => _byCode.Values;

    public Result<Currency> Get(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<Currency>(
                Error.Validation("currency.code.missing", "A currency code is required."));
        }

        return _byCode.TryGetValue(code.ToUpperInvariant(), out Currency? currency)
            ? currency
            : Result.Failure<Currency>(
                Error.NotFound("currency.not_configured", $"Currency '{code}' is not configured."));
    }
}
