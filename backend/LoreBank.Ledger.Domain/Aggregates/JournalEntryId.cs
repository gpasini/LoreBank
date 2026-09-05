using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Domain.Aggregates;

public sealed class JournalEntryId(Guid value) : SimpleValueObject<Guid>(value)
{
    public static JournalEntryId New() => new(Guid.NewGuid());
}
