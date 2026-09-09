namespace LoreBank.Ledger.Infrastructure.Persistence.ReadRows;

// Le miroir plat de journal_entries, réservé à la lecture : l'écriture
// elle-même, que les jambes rejoignent par leur FK — une row par table, le
// reader compose la jointure en LINQ.
public sealed record JournalEntryRow(
    Guid Id,
    DateTimeOffset RecordedAt
);
