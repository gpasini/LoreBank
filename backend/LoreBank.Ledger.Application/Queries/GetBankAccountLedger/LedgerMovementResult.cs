namespace LoreBank.Ledger.Application.Queries.GetBankAccountLedger;

// Une jambe d'écriture vue du compte : peuplée colonne par colonne par le
// reader, sans dépendance au modèle d'écriture.
public sealed record LedgerMovementResult(
    Guid EntryId,
    string Direction,
    decimal Amount,
    string Currency
);
