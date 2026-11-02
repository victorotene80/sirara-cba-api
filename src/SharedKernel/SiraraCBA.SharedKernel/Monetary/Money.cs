namespace SiraraCBA.SharedKernel.Monetary;

/// <summary>
/// An amount in a specific currency. Immutable, and closed under arithmetic only
/// within one currency.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    /// <summary>
    /// Creates an amount, rejecting more precision than the currency permits.
    /// </summary>
    /// <remarks>
    /// Rounding is refused rather than applied: silently rounding a caller's
    /// amount is how a ledger loses money it can never account for. The caller
    /// decides how to round, explicitly, before it gets here.
    /// </remarks>
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        int scale = ScaleOf(amount);
        if (scale > currency.MinorUnits)
        {
            return Result.Failure<Money>(Error.Validation(
                "money.precision.exceeded",
                $"{amount} has {scale} decimal places; {currency.Code} permits {currency.MinorUnits}."));
        }

        return new Money(amount, currency);
    }

    /// <summary>Zero in the given currency.</summary>
    public static Money Zero(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new Money(decimal.Zero, currency);
    }

    public bool IsZero => Amount == decimal.Zero;

    public bool IsPositive => Amount > decimal.Zero;

    public bool IsNegative => Amount < decimal.Zero;

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Negate(Money value) => -value;

    public override string ToString() =>
        Amount.ToString($"F{Currency.MinorUnits}", System.Globalization.CultureInfo.InvariantCulture)
        + " " + Currency.Code;

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!left.Currency.Equals(right.Currency))
        {
            throw new CurrencyMismatchException(left.Currency, right.Currency);
        }
    }

    private static int ScaleOf(decimal value) => (decimal.GetBits(value)[3] >> ScaleByteOffset) & ScaleMask;

    // Layout of decimal's flags word, defined by the CLR — not a business number.
    private const int ScaleByteOffset = 16;
    private const int ScaleMask = 0xFF;
}
