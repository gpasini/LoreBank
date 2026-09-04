using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.Bank.Test.Unit.Builders;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.Domain.BankAccounts;

[TestFixture]
[TestOf(typeof(BankAccount))]
public sealed class DepositTest
{
    [Test]
    public void Deposit_ShouldIncreaseBalance_AndEmitMoneyDeposited()
    {
        // Arrange

        var account = new BankAccountBuilder()
            .WithBalance(50m)
            .Build();

        // Act

        account.Deposit(
            new PositiveMoney(
                amount: 100m,
                currency: "EUR"
            )
        );

        // Assert

        account.Balance.Amount.Should().Be(150m);
        account.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is MoneyDeposited);
    }

    [Test]
    public void Deposit_ShouldThrow_WhenCurrencyDiffers()
    {
        // Arrange

        var account = new BankAccountBuilder().Build();

        // Act & Assert

        var act = () => account.Deposit(
            new PositiveMoney(
                amount: 10m,
                currency: "USD"
            )
        );

        act.Should().Throw<CurrencyMismatchException>();
    }

    [Test]
    public void Deposit_ShouldThrow_WhenAccountIsClosed()
    {
        // Arrange

        var account = new BankAccountBuilder()
            .Closed()
            .Build();

        // Act & Assert

        var act = () => account.Deposit(
            new PositiveMoney(
                amount: 10m,
                currency: "EUR"
            )
        );

        act.Should().Throw<AccountClosedException>();
    }
}
