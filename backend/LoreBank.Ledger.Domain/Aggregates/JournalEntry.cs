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
        IReadOnlyList<JournalLine> lines
    ) : base(id)
    {
        _lines = [.. lines];
    }

    // Réservé à la matérialisation EF Core, qui peuple ensuite la collection.
    private JournalEntry() : base(null!)
    {
        _lines = [];
    }

    public IReadOnlyList<JournalLine> Lines => _lines;

    public static JournalEntry Record(IReadOnlyList<JournalLine> lines)
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
            lines: lines
        );

        entry.AddDomainEvent(new JournalEntryRecordedDomainEvent(entry.Id));

        return entry;
    }

    private static Money SumOf(
        IReadOnlyList<JournalLine> lines,
        JournalDirection direction
    ) => lines
        .Where(line => line.Direction == direction)
        .Aggregate(
            seed: new Money(
                amount: 0m,
                currency: lines[0].Amount.Currency
            ),
            func: (
                total,
                line
            ) => total + line.Amount
        );
}
