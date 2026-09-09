namespace LoreBank.Ledger.Application.Queries.GetBankAccountLedger;

// Une jambe d'écriture vue du compte : peuplée colonne par colonne par le
// reader, sans dépendance au modèle d'écriture. RecordedAt est l'Instant de
// comptabilisation de l'écriture (ADR 0024), l'ordre de la liste.
public sealed record LedgerMovementResult(
    Guid EntryId,
    string Direction,
    decimal Amount,
    string Currency,
    DateTimeOffset RecordedAt
);
