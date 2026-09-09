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
    private static readonly DateTimeOffset Instant = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 8,
        minute: 30,
        second: 0,
        offset: TimeSpan.Zero
    );

    [Test]
    public void Record_ShouldCreateTheEntry_AndEmitJournalEntryRecordedDomainEvent()
    {
        // Arrange

        var lines = BalancedLines(25.50m);

        // Act

        var entry = JournalEntry.Record(
            lines: lines,
            recordedAt: Instant
        );

        // Assert

        entry.Lines.Should().Equal(lines);
        entry.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is JournalEntryRecordedDomainEvent)
            .Which.As<JournalEntryRecordedDomainEvent>().RecordedAt.Should().Be(Instant);
    }

    // L'Instant est reçu, jamais demandé (ADR 0024) : l'écriture enregistre
    // celui qu'on lui passe, aucune horloge n'est consultée.
    [Test]
    public void Record_ShouldRecordTheInstant()
    {
        var entry = JournalEntry.Record(
            lines: BalancedLines(10m),
            recordedAt: Instant
        );

        entry.RecordedAt.Should().Be(Instant);
    }

    [Test]
    public void Record_ShouldThrow_WhenTheEntryIsEmpty()
    {
        var act = () => JournalEntry.Record(
            lines: [],
            recordedAt: Instant
        );

        act.Should().Throw<EmptyJournalEntryException>();
    }

    [Test]
    public void Record_ShouldThrow_WhenDebitsAndCreditsDiffer()
    {
        var act = () => JournalEntry.Record(
            lines: [
                Line(
                    direction: JournalDirection.Debit,
                    amount: 25.50m
                ),
                Line(
                    direction: JournalDirection.Credit,
                    amount: 20m
                ),
            ],
            recordedAt: Instant
        );

        act.Should().Throw<UnbalancedJournalEntryException>();
    }

    // Une seule jambe : le total de l'autre sens vaut zéro, l'écriture ne peut
    // pas s'équilibrer.
    [Test]
    public void Record_ShouldThrow_WhenAllLinesShareTheSameDirection()
    {
        var act = () => JournalEntry.Record(
            lines: [
                Line(
                    direction: JournalDirection.Debit,
                    amount: 25.50m
                ),
            ],
            recordedAt: Instant
        );

        act.Should().Throw<UnbalancedJournalEntryException>();
    }

    // L'invariant de devise vit dans Money : deux devises dans une même
    // écriture lèvent son exception, rien n'est revérifié dans l'agrégat.
    [Test]
    public void Record_ShouldThrow_WhenLinesMixCurrencies()
    {
        var act = () => JournalEntry.Record(
            lines: [
                Line(
                    direction: JournalDirection.Debit,
                    amount: 10m
                ),
                JournalLine.Of(
                    account: LedgerAccountRef.Cash,
                    direction: JournalDirection.Credit,
                    amount: PositiveMoney.Of(
                        amount: 10m,
                        currency: "USD"
                    )
                ),
            ],
            recordedAt: Instant
        );

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
    ) => JournalLine.Of(
        account: direction == JournalDirection.Debit
            ? LedgerAccountRef.Cash
            : LedgerAccountRef.ForBankAccount(Guid.NewGuid()),
        direction: direction,
        amount: PositiveMoney.Of(
            amount: amount,
            currency: "EUR"
        )
    );
}
