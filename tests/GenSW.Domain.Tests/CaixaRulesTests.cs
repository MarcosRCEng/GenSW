using GenSW.Domain.Financial;
using Xunit;
namespace GenSW.Domain.Tests;
public sealed class CaixaRulesTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("9999999999999999.99")]
    public void Exact_positive_money(string value) => Assert.Equal(decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture),CaixaRules.Dinheiro(decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture),true));
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public void Invalid_money_is_not_rounded(string value) => Assert.Throws<ArgumentException>(()=>CaixaRules.Dinheiro(decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture),true));
    [Fact]
    public void Leap_year_month_boundaries_and_operational_date()
    {
        Assert.Equal(new DateOnly(2024,2,1),CaixaRules.Mes(new(2024,2,29)));
        Assert.Equal(new DateOnly(2025,1,1),CaixaRules.Mes(new(2024,12,31)).AddMonths(1));
        Assert.Equal(new DateOnly(2026,10,1),CaixaRules.Hoje(new Clock()));
        CaixaRules.Data(new(2024,2,29),new(2024,2,1),new(2024,3,1));
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,10,2,2,59,59,TimeSpan.Zero); }
}
