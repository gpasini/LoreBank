using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.Bank.Test.Unit.Builders;

namespace LoreBank.Bank.Test.Unit.Domain.BankAccounts;

[TestFixture]
[TestOf(typeof(BankAccount))]
public sealed class CloseTest
{
    [Test]
    public void Close_ShouldCloseAccount_AndEmitBankAccountClosed()
    {
        // Arrange

        var account = new BankAccountBuilder().Build();

        // Act

        account.Close();

        // Assert

        account.IsClosed.Should().BeTrue();
        account.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is BankAccountClosed);
    }

    [Test]
    public void Close_ShouldThrow_WhenBalanceIsNotZero()
    {
        // Arrange

        var account = new BankAccountBuilder()
            .WithBalance(70m)
            .Build();

        // Act & Assert

        var act = () => account.Close();

        act.Should().Throw<NonEmptyAccountClosureException>();
    }

    [Test]
    public void Close_ShouldThrow_WhenAccountIsAlreadyClosed()
    {
        // Arrange

        var account = new BankAccountBuilder()
            .Closed()
            .Build();

        // Act & Assert

        var act = () => account.Close();

        act.Should().Throw<AccountClosedException>();
    }
}
