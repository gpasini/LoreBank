using LoreBank.Ledger.Application.Exceptions;
using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Test.Unit.Exceptions;

// Le nom d'une exception EST son contrat public (Code en est dérivé) : ces
// tests épinglent les codes publiés pour qu'un renommage fasse échouer la
// suite plutôt que de livrer silencieusement une rupture de contrat.
[TestFixture]
public sealed class ExceptionCodesTest
{
    [Test]
    [TestOf(typeof(UnbalancedJournalEntryException))]
    public void Code_ShouldBeLedgerUnbalancedJournalEntry_WhenExceptionIsUnbalancedJournalEntryException()
    {
        var exception = new UnbalancedJournalEntryException(
            debits: new Money(
                amount: 25.50m,
                currency: "EUR"
            ),
            credits: new Money(
                amount: 20m,
                currency: "EUR"
            )
        );

        exception.Code.Should().Be("LEDGER.UNBALANCED_JOURNAL_ENTRY");
    }

    [Test]
    [TestOf(typeof(EmptyJournalEntryException))]
    public void Code_ShouldBeLedgerEmptyJournalEntry_WhenExceptionIsEmptyJournalEntryException()
    {
        new EmptyJournalEntryException().Code.Should().Be("LEDGER.EMPTY_JOURNAL_ENTRY");
    }

    [Test]
    [TestOf(typeof(InvalidLedgerAccountRefException))]
    public void Code_ShouldBeLedgerInvalidLedgerAccountRef_WhenExceptionIsInvalidLedgerAccountRefException()
    {
        new InvalidLedgerAccountRefException("CAISSE").Code.Should().Be("LEDGER.INVALID_LEDGER_ACCOUNT_REF");
    }

    [Test]
    [TestOf(typeof(UnknownBankAccountException))]
    public void Code_ShouldBeLedgerUnknownBankAccount_WhenExceptionIsUnknownBankAccountException()
    {
        new UnknownBankAccountException(Guid.NewGuid()).Code.Should().Be("LEDGER.UNKNOWN_BANK_ACCOUNT");
    }

    [Test]
    [TestOf(typeof(JournalEntryNotFoundException))]
    public void Code_ShouldBeLedgerJournalEntryNotFound_WhenExceptionIsJournalEntryNotFoundException()
    {
        new JournalEntryNotFoundException(JournalEntryId.New()).Code.Should().Be("LEDGER.JOURNAL_ENTRY_NOT_FOUND");
    }
}
