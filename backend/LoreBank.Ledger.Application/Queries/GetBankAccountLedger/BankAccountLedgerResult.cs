namespace LoreBank.Ledger.Application.Queries.GetBankAccountLedger;

// L'iban vient du port de lecture publié par Bank (le Ledger ne stocke que
// l'id), les mouvements du reader du module : ce Result est le point de
// rencontre des deux canaux de communication.
public sealed record BankAccountLedgerResult(
    Guid AccountId,
    string Iban,
    IReadOnlyList<LedgerMovementResult> Movements
);
