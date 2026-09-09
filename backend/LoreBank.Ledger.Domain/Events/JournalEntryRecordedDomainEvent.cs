using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.Ledger.Domain.Events;

public sealed record JournalEntryRecordedDomainEvent(
    JournalEntryId EntryId,
    DateTimeOffset RecordedAt
) : IDomainEvent;
