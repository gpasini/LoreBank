using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.Bank.Test.Unit.Builders;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.Domain.BankAccounts;

[TestFixture]
[TestOf(typeof(BankAccount))]
public sealed class WithdrawTest
{
    [Test]
    public void Withdraw_ShouldDecreaseBalance_AndEmitMoneyWithdrawn()
    {
        // Arrange

        var account = new BankAccountBuilder()
            .WithBalance(100m)
            .Build();

        // Act

        account.Withdraw(
            new PositiveMoney(
                amount: 30m,
                currency: "EUR"
            )
        );

        // Assert

        account.Balance.Amount.Should().Be(70m);
        account.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is MoneyWithdrawnDomainEvent);
    }

    [Test]
    public void Withdraw_ShouldThrow_WhenBalanceIsInsufficient()
    {
        // Arrange

        var account = new BankAccountBuilder()
            .WithBalance(70m)
            .Build();

        // Act & Assert

        var act = () => account.Withdraw(
            new PositiveMoney(
                amount: 1000m,
                currency: "EUR"
            )
        );

        act.Should().Throw<InsufficientBalanceException>();
    }

    [Test]
    public void Withdraw_ShouldThrow_WhenAccountIsClosed()
    {
        // Arrange

        var account = new BankAccountBuilder()
            .Closed()
            .Build();

        // Act & Assert

        var act = () => account.Withdraw(
            new PositiveMoney(
                amount: 1m,
                currency: "EUR"
            )
        );

        act.Should().Throw<AccountClosedException>();
    }
}
