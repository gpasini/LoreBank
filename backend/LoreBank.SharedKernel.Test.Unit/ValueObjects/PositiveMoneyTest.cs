using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(PositiveMoney))]
public sealed class PositiveMoneyTest
{
    [Test]
    public void Of_ShouldExposeTheMoney_WhenAmountIsPositive()
    {
        var amount = PositiveMoney.Of(
            amount: 100m,
            currency: "EUR"
        );

        amount.Value.Should().Be(
            Money.Of(
                amount: 100m,
                currency: "EUR"
            )
        );
    }

    [Test]
    public void Of_ShouldThrow_WhenAmountIsNegative()
    {
        var act = () => PositiveMoney.Of(
            amount: -100m,
            currency: "EUR"
        );

        act.Should().Throw<NonPositiveAmountException>();
    }

    [Test]
    public void Of_ShouldThrow_WhenAmountIsZero()
    {
        // Un dépôt ou un retrait de zéro n'est pas une opération : strictement
        // positif, pas seulement non négatif.
        var act = () => PositiveMoney.Of(
            amount: 0m,
            currency: "EUR"
        );

        act.Should().Throw<NonPositiveAmountException>();
    }

    [Test]
    public void Of_ShouldThrow_WhenCurrencyIsInvalid()
    {
        // La devise est déléguée à Money : même exception, pas de seconde règle.
        var act = () => PositiveMoney.Of(
            amount: 10m,
            currency: "eur"
        );

        act.Should().Throw<InvalidCurrencyException>();
    }

    [Test]
    public void Equals_ShouldBeTrue_WhenAmountAndCurrencyMatch()
    {
        var left = PositiveMoney.Of(
            amount: 10m,
            currency: "EUR"
        );
        var right = PositiveMoney.Of(
            amount: 10m,
            currency: "EUR"
        );

        left.Should().Be(right);
    }
}
