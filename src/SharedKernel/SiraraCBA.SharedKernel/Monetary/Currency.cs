namespace SiraraCBA.SharedKernel.Monetary;

/// <summary>
/// An ISO 4217 currency and the number of minor units it is expressed in.
/// </summary>
/// <remarks>
/// <see cref="MinorUnits"/> exists so that no part of the system assumes two
/// decimal places. JPY has zero, KWD has three, and a ledger that hardcodes
/// <c>2</c> silently corrupts both (requirements.md, no magic numbers; ER-9).
/// </remarks>
public sealed record Currency
{
    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    /// <summary>The three-letter ISO 4217 alphabetic code, uppercase.</summary>
    public string Code { get; }

    /// <summary>Decimal places in which amounts of this currency are expressed.</summary>
    public int MinorUnits { get; }

    /// <summary>
    /// Creates a currency definition. Used by the registry when loading seed data;
    /// application code resolves currencies through <see cref="ICurrencyRegistry"/>
    /// rather than constructing them, so that an unknown code fails loudly in one
    /// place.
    /// </summary>
    public static Result<Currency> Create(string code, int minorUnits)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != IsoAlphabeticCodeLength)
        {
            return Result.Failure<Currency>(
                Error.Validation("currency.code.invalid", $"'{code}' is not an ISO 4217 alphabetic code."));
        }

        if (!code.All(char.IsAsciiLetterUpper))
        {
            return Result.Failure<Currency>(
                Error.Validation("currency.code.invalid", $"'{code}' must be uppercase ASCII letters."));
        }

        if (minorUnits < 0)
        {
            return Result.Failure<Currency>(
                Error.Validation("currency.minorunits.invalid", "Minor units cannot be negative."));
        }

        return new Currency(code, minorUnits);
    }

    public override string ToString() => Code;

    /// <summary>
    /// The length of an ISO 4217 alphabetic code. Intrinsic to the standard, not a
    /// business threshold — named rather than inlined.
    /// </summary>
    private const int IsoAlphabeticCodeLength = 3;
}
