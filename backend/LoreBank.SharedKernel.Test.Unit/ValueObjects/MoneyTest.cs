using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(Money))]
public sealed class MoneyTest
{
    [Test]
    public void Add_ShouldSumAmounts_WhenCurrenciesMatch()
    {
        // Arrange

        var left = Money.Of(
            amount: 10.50m,
            currency: "EUR"
        );

        var right = Money.Of(
            amount: 4.50m,
            currency: "EUR"
        );

        // Act

        var sum = left + right;

        // Assert

        sum.Should().Be(
            Money.Of(
                amount: 15m,
                currency: "EUR"
            )
        );
    }

    [Test]
    public void Subtract_ShouldAllowNegativeResult()
    {
        // Act

        var result = Money.Of(
            amount: 10m,
            currency: "EUR"
        ) - Money.Of(
            amount: 15m,
            currency: "EUR"
        );

        // Assert

        result.Amount.Should().Be(-5m);
    }

    [Test]
    public void Add_ShouldThrow_WhenCurrenciesDiffer()
    {
        // Arrange

        var euros = Money.Of(
            amount: 10m,
            currency: "EUR"
        );

        var dollars = Money.Of(
            amount: 5m,
            currency: "USD"
        );

        // Act & Assert

        var act = () => euros + dollars;

        act.Should().Throw<CurrencyMismatchException>();
    }

    [TestCase("euro")]
    [TestCase("EU")]
    [TestCase("EURO")]
    [TestCase("")]
    public void Of_ShouldThrow_WhenCurrencyIsInvalid(string currency)
    {
        var act = () => Money.Of(
            amount: 1m,
            currency: currency
        );

        act.Should().Throw<InvalidCurrencyException>();
    }

    [Test]
    public void Equals_ShouldIgnoreTrailingZeros()
    {
        Money.Of(
            amount: 10m,
            currency: "EUR"
        ).Should().Be(
            Money.Of(
                amount: 10.00m,
                currency: "EUR"
            )
        );
    }
}
