using System.Diagnostics.CodeAnalysis;

namespace SiraraCBA.SharedKernel.UnitTests.Monetary;

public sealed class CurrencyRegistryTests
{
    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification =
            "Typed as the interface on purpose. Application code resolves currencies " +
            "through ICurrencyRegistry (ER-9), so that is the surface under test; " +
            "binding this to InMemoryCurrencyRegistry would stop exercising the " +
            "contract. Devirtualisation has no meaning in a suite that runs in " +
            "milliseconds.")]
    private static readonly ICurrencyRegistry Registry =
        new InMemoryCurrencyRegistry([TestCurrencies.Ngn, TestCurrencies.Jpy, TestCurrencies.Kwd]);

    [Fact]
    public void Get_WhenConfigured_ReturnsTheCurrencyWithItsMinorUnits()
    {
        Result<Currency> result = Registry.Get("JPY");

        result.IsSuccess.ShouldBeTrue();
        result.Value.MinorUnits.ShouldBe(0);
    }

    [Fact]
    public void Get_IsCaseInsensitiveOnTheCode()
    {
        Registry.Get("ngn").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Get_WhenNotConfigured_FailsRatherThanInventingADefault()
    {
        Result<Currency> result = Registry.Get("XAF");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void Construction_WithNoCurrencies_FailsImmediately()
    {
        // Booting with nothing configured would fail every posting later, far from
        // the cause. Fail at startup instead.
        Should.Throw<InvalidOperationException>(() => new InMemoryCurrencyRegistry([]));
    }
}
