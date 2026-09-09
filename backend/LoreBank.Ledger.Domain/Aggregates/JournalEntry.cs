using LoreBank.Ledger.Domain.Events;
using LoreBank.Ledger.Domain.Exceptions;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.SharedKernel.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Domain.Aggregates;

// Une écriture en partie double : des jambes débit/crédit qui s'équilibrent —
// Σ débits == Σ crédits, même devise. L'invariant vit dans la factory : une
// écriture déséquilibrée ne peut pas naître, et une écriture née ne mute
// plus — un fait comptable enregistré ne se corrige que par une écriture
// inverse, jamais par une rature.
public sealed class JournalEntry : AggregateRoot<JournalEntryId>
{
    private readonly List<JournalLine> _lines;

    private JournalEntry(
        JournalEntryId id,
        IReadOnlyList<JournalLine> lines,
        DateTimeOffset recordedAt
    ) : base(id)
    {
        _lines = [.. lines];
        RecordedAt = recordedAt;
    }

    // Réservé à la matérialisation EF Core, qui peuple ensuite la collection.
    private JournalEntry() : base(null!)
    {
        _lines = [];
    }

    public IReadOnlyList<JournalLine> Lines => _lines;

    // L'Instant de comptabilisation (ADR 0024) : reçu de la transition, jamais
    // demandé — celui de l'enregistrement par le Ledger, pas celui du fait
    // chez Bank.
    public DateTimeOffset RecordedAt { get; }

    public static JournalEntry Record(
        IReadOnlyList<JournalLine> lines,
        DateTimeOffset recordedAt
    )
    {
        if (lines.Count == 0) {
            throw new EmptyJournalEntryException();
        }

        // Les sommes passent par Money : deux devises dans la même écriture
        // lèvent la CurrencyMismatchException du VO — rien à revérifier ici.
        var debits = SumOf(
            lines: lines,
            direction: JournalDirection.Debit
        );
        var credits = SumOf(
            lines: lines,
            direction: JournalDirection.Credit
        );

        if (!debits.Equals(credits)) {
            throw new UnbalancedJournalEntryException(
                debits: debits,
                credits: credits
            );
        }

        var entry = new JournalEntry(
            id: JournalEntryId.New(),
            lines: lines,
            recordedAt: recordedAt
        );

        entry.AddDomainEvent(
            new JournalEntryRecordedDomainEvent(
                EntryId: entry.Id,
                RecordedAt: recordedAt
            )
        );

        return entry;
    }

    private static Money SumOf(
        IReadOnlyList<JournalLine> lines,
        JournalDirection direction
    ) => lines
        .Where(line => line.Direction == direction)
        .Aggregate(
            seed: Money.Of(
                amount: 0m,
                currency: lines[0].Amount.Currency
            ),
            func: (
                total,
                line
            ) => total + line.Amount
        );
}
