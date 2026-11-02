namespace SiraraCBA.SharedKernel.UnitTests.Monetary;

public sealed class MoneyTests
{
    private static Money Ngn(decimal amount) => Money.Create(amount, TestCurrencies.Ngn).Value;

    [Fact]
    public void Add_AcrossDifferentCurrencies_IsRefused()
    {
        Money ngn = Ngn(100m);
        Money kes = Money.Create(100m, TestCurrencies.Kes).Value;

        CurrencyMismatchException exception = Should.Throw<CurrencyMismatchException>(() => ngn + kes);

        exception.Left.ShouldBe(TestCurrencies.Ngn);
        exception.Right.ShouldBe(TestCurrencies.Kes);
    }

    [Fact]
    public void Subtract_AcrossDifferentCurrencies_IsRefused()
    {
        Money ngn = Ngn(100m);
        Money kes = Money.Create(1m, TestCurrencies.Kes).Value;

        Should.Throw<CurrencyMismatchException>(() => ngn - kes);
    }

    [Fact]
    public void Compare_AcrossDifferentCurrencies_IsRefused()
    {
        Money ngn = Ngn(100m);
        Money kes = Money.Create(1m, TestCurrencies.Kes).Value;

        Should.Throw<CurrencyMismatchException>(() => ngn > kes);
    }

    [Fact]
    public void Create_WithMorePrecisionThanTheCurrencyPermits_IsRefusedRatherThanRounded()
    {
        // Silently rounding is how a ledger loses money it can never account for.
        Result<Money> result = Money.Create(100.005m, TestCurrencies.Ngn);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("money.precision.exceeded");
    }

    [Fact]
    public void Create_ForAZeroMinorUnitCurrency_RefusesAnyFraction()
    {
        Money.Create(100.5m, TestCurrencies.Jpy).IsFailure.ShouldBeTrue();
        Money.Create(100m, TestCurrencies.Jpy).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_ForAThreeMinorUnitCurrency_AcceptsThreeDecimals()
    {
        // The scale comes from the registry, not from an assumed 2.
        Money.Create(100.123m, TestCurrencies.Kwd).IsSuccess.ShouldBeTrue();
        Money.Create(100.1234m, TestCurrencies.Kwd).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Add_WithinOneCurrency_Sums()
    {
        (Ngn(100.25m) + Ngn(0.75m)).Amount.ShouldBe(101.00m);
    }

    [Fact]
    public void ToString_RendersTheCurrencysOwnScale()
    {
        Money.Create(100m, TestCurrencies.Jpy).Value.ToString().ShouldBe("100 JPY");
        Ngn(100m).ToString().ShouldBe("100.00 NGN");
        Money.Create(100m, TestCurrencies.Kwd).Value.ToString().ShouldBe("100.000 KWD");
    }

    [Fact]
    public void Zero_IsZeroInThatCurrency()
    {
        Money zero = Money.Zero(TestCurrencies.Ngn);

        zero.IsZero.ShouldBeTrue();
        zero.Currency.ShouldBe(TestCurrencies.Ngn);
    }
}
