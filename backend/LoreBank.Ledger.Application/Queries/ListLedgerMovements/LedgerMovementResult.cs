namespace LoreBank.Ledger.Application.Queries.ListLedgerMovements;

// Une jambe d'écriture vue du compte, l'item de la Liste des mouvements :
// peuplée colonne par colonne par le reader, sans dépendance au modèle
// d'écriture. RecordedAt est l'Instant de comptabilisation de l'écriture
// (ADR 0024), l'ordre de la Liste — du plus récent au plus ancien.
public sealed record LedgerMovementResult(
    Guid EntryId,
    string Direction,
    decimal Amount,
    string Currency,
    DateTimeOffset RecordedAt
);
