namespace LoreBank.Ledger.Infrastructure.Persistence.ReadRows;

// Le miroir plat de journal_lines, réservé à la lecture : la FK vers l'entry
// et l'enum de direction, invisibles du domaine (shadow property et VO côté
// écriture), sont ici de vraies propriétés primitives — une row reflète la
// table, pas le modèle d'écriture.
public sealed record JournalLineRow(
    Guid JournalEntryId,
    string AccountRef,
    string Direction,
    decimal Amount,
    string Currency
);
