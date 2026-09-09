namespace LoreBank.Bank.Application.Queries.GetBankAccountById;

// Aucune dépendance au modèle d'écriture : ce DTO est peuplé colonne par colonne
// par le reader de l'Infrastructure, jamais projeté depuis l'agrégat.
public sealed record BankAccountResult(
    Guid Id,
    string Iban,
    decimal Balance,
    string Currency,
    bool IsClosed,
    // L'Acteur qui a ouvert le compte ; null pour un Anonyme (ADR 0023).
    string? OpenedBy
);
