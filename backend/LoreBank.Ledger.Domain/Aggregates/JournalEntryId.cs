using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Domain.Aggregates;

public sealed class JournalEntryId : SimpleValueObject<Guid>
{
    private JournalEntryId(Guid value) : base(value)
    {
    }

    public static JournalEntryId New() => new(Guid.NewGuid());

    public static JournalEntryId Hydrate(Guid value) => new(value);
}
