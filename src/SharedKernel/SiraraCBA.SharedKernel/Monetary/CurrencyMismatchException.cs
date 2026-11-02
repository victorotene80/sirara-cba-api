namespace SiraraCBA.SharedKernel.Monetary;

/// <summary>
/// Thrown when arithmetic is attempted across two currencies.
/// </summary>
/// <remarks>
/// This is an exception rather than a <see cref="Result"/> deliberately. Adding
/// NGN to KES is not a business outcome a caller can handle — it is a bug in the
/// caller. Converting between currencies is an explicit FX operation with a rate,
/// never an implicit coercion (ledger-spec.md I-2).
/// </remarks>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    public CurrencyMismatchException(Currency left, Currency right)
        : base($"Cannot operate on {left.Code} and {right.Code} together. Convert explicitly through an FX rate.")
    {
        Left = left;
        Right = right;
    }

    public Currency Left { get; }

    public Currency Right { get; }
}
