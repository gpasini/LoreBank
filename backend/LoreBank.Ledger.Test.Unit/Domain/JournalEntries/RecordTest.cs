using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.Events;
using LoreBank.Ledger.Domain.Exceptions;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Test.Unit.Domain.JournalEntries;

[TestFixture]
[TestOf(typeof(JournalEntry))]
public sealed class RecordTest
{
    [Test]
    public void Record_ShouldCreateTheEntry_AndEmitJournalEntryRecordedDomainEvent()
    {
        // Arrange

        var lines = BalancedLines(25.50m);

        // Act

        var entry = JournalEntry.Record(lines);

        // Assert

        entry.Lines.Should().Equal(lines);
        entry.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is JournalEntryRecordedDomainEvent);
    }

    [Test]
    public void Record_ShouldThrow_WhenTheEntryIsEmpty()
    {
        var act = () => JournalEntry.Record([]);

        act.Should().Throw<EmptyJournalEntryException>();
    }

    [Test]
    public void Record_ShouldThrow_WhenDebitsAndCreditsDiffer()
    {
        var act = () => JournalEntry.Record([
            Line(
                direction: JournalDirection.Debit,
                amount: 25.50m
            ),
            Line(
                direction: JournalDirection.Credit,
                amount: 20m
            ),
        ]);

        act.Should().Throw<UnbalancedJournalEntryException>();
    }

    // Une seule jambe : le total de l'autre sens vaut zéro, l'écriture ne peut
    // pas s'équilibrer.
    [Test]
    public void Record_ShouldThrow_WhenAllLinesShareTheSameDirection()
    {
        var act = () => JournalEntry.Record([
            Line(
                direction: JournalDirection.Debit,
                amount: 25.50m
            ),
        ]);

        act.Should().Throw<UnbalancedJournalEntryException>();
    }

    // L'invariant de devise vit dans Money : deux devises dans une même
    // écriture lèvent son exception, rien n'est revérifié dans l'agrégat.
    [Test]
    public void Record_ShouldThrow_WhenLinesMixCurrencies()
    {
        var act = () => JournalEntry.Record([
            Line(
                direction: JournalDirection.Debit,
                amount: 10m
            ),
            new JournalLine(
                account: LedgerAccountRef.Cash,
                direction: JournalDirection.Credit,
                amount: new PositiveMoney(
                    amount: 10m,
                    currency: "USD"
                )
            ),
        ]);

        act.Should().Throw<CurrencyMismatchException>();
    }

    private static IReadOnlyList<JournalLine> BalancedLines(decimal amount) => [
        Line(
            direction: JournalDirection.Debit,
            amount: amount
        ),
        Line(
            direction: JournalDirection.Credit,
            amount: amount
        ),
    ];

    private static JournalLine Line(
        JournalDirection direction,
        decimal amount
    ) => new(
        account: direction == JournalDirection.Debit
            ? LedgerAccountRef.Cash
            : LedgerAccountRef.ForBankAccount(Guid.NewGuid()),
        direction: direction,
        amount: new PositiveMoney(
            amount: amount,
            currency: "EUR"
        )
    );
}
