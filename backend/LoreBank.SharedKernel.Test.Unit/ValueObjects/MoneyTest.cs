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

        var left = new Money(
            amount: 10.50m,
            currency: "EUR"
        );

        var right = new Money(
            amount: 4.50m,
            currency: "EUR"
        );

        // Act

        var sum = left + right;

        // Assert

        sum.Should().Be(
            new Money(
                amount: 15m,
                currency: "EUR"
            )
        );
    }

    [Test]
    public void Subtract_ShouldAllowNegativeResult()
    {
        // Act

        var result = new Money(
            amount: 10m,
            currency: "EUR"
        ) - new Money(
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

        var euros = new Money(
            amount: 10m,
            currency: "EUR"
        );

        var dollars = new Money(
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
    public void Constructor_ShouldThrow_WhenCurrencyIsInvalid(string currency)
    {
        var act = () => new Money(
            amount: 1m,
            currency: currency
        );

        act.Should().Throw<InvalidCurrencyException>();
    }

    [Test]
    public void Equals_ShouldIgnoreTrailingZeros()
    {
        new Money(
            amount: 10m,
            currency: "EUR"
        ).Should().Be(
            new Money(
                amount: 10.00m,
                currency: "EUR"
            )
        );
    }
}
