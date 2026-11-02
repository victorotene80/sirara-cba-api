namespace SiraraCBA.SharedKernel.Monetary;

/// <summary>
/// Resolves currency definitions. The single place that knows how many minor units
/// a currency has, so that number is never a literal anywhere else (ER-9).
/// </summary>
public interface ICurrencyRegistry
{
    /// <summary>Resolves a currency by ISO 4217 code, or fails if it is not configured.</summary>
    public Result<Currency> Get(string code);

    /// <summary>Every currency this deployment is configured to handle.</summary>
    public IReadOnlyCollection<Currency> All { get; }
}
