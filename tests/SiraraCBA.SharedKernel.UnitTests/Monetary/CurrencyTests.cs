namespace SiraraCBA.SharedKernel.UnitTests.Monetary;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("ngn")]
    [InlineData("NG")]
    [InlineData("NGNN")]
    [InlineData("N1G")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenCodeIsNotIso4217Alphabetic_Fails(string code)
    {
        Result<Currency> result = Currency.Create(code, minorUnits: 2);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("currency.code.invalid");
    }

    [Fact]
    public void Create_WhenMinorUnitsIsNegative_Fails()
    {
        Result<Currency> result = Currency.Create("NGN", minorUnits: -1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Theory]
    [InlineData("NGN", 2)]
    [InlineData("JPY", 0)]
    [InlineData("KWD", 3)]
    public void Create_WhenValid_CarriesTheConfiguredMinorUnits(string code, int minorUnits)
    {
        Result<Currency> result = Currency.Create(code, minorUnits);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe(code);
        result.Value.MinorUnits.ShouldBe(minorUnits);
    }
}
