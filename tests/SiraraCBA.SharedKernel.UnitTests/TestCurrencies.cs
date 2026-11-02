namespace SiraraCBA.SharedKernel.UnitTests;

/// <summary>
/// Currencies used by the tests. Chosen to span zero, two and three minor units,
/// because a system that only ever sees two-decimal currencies will happily
/// hardcode two.
/// </summary>
internal static class TestCurrencies
{
    public static readonly Currency Ngn = Currency.Create("NGN", 2).Value;
    public static readonly Currency Kes = Currency.Create("KES", 2).Value;
    public static readonly Currency Jpy = Currency.Create("JPY", 0).Value;
    public static readonly Currency Kwd = Currency.Create("KWD", 3).Value;
}
