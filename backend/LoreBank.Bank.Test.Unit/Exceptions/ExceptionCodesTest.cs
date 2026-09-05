using LoreBank.Bank.Application.Exceptions;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.Exceptions;

// Le nom d'une exception EST son contrat public (Code en est dérivé) : ces
// tests épinglent les codes publiés pour qu'un renommage fasse échouer la
// suite plutôt que de livrer silencieusement une rupture de contrat.
[TestFixture]
public sealed class ExceptionCodesTest
{
    [Test]
    [TestOf(typeof(InsufficientBalanceException))]
    public void Code_ShouldBeBankInsufficientBalance_WhenExceptionIsInsufficientBalanceException()
    {
        var exception = new InsufficientBalanceException(
            balance: Money.Of(
                amount: 10m,
                currency: "EUR"
            ),
            requested: Money.Of(
                amount: 20m,
                currency: "EUR"
            )
        );

        exception.Code.Should().Be("BANK.INSUFFICIENT_BALANCE");
    }

    [Test]
    [TestOf(typeof(AccountClosedException))]
    public void Code_ShouldBeBankAccountClosed_WhenExceptionIsAccountClosedException()
    {
        var exception = new AccountClosedException(BankAccountId.New());

        exception.Code.Should().Be("BANK.ACCOUNT_CLOSED");
    }

    [Test]
    [TestOf(typeof(NonEmptyAccountClosureException))]
    public void Code_ShouldBeBankNonEmptyAccountClosure_WhenExceptionIsNonEmptyAccountClosureException()
    {
        var exception = new NonEmptyAccountClosureException(
            accountId: BankAccountId.New(),
            balance: Money.Of(
                amount: 10m,
                currency: "EUR"
            )
        );

        exception.Code.Should().Be("BANK.NON_EMPTY_ACCOUNT_CLOSURE");
    }

    [Test]
    [TestOf(typeof(BankAccountNotFoundException))]
    public void Code_ShouldBeBankBankAccountNotFound_WhenExceptionIsBankAccountNotFoundException()
    {
        var exception = new BankAccountNotFoundException(BankAccountId.New());

        exception.Code.Should().Be("BANK.BANK_ACCOUNT_NOT_FOUND");
    }
}
