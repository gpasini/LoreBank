namespace LoreBank.Bank.Application.Queries.ListBankAccounts;

// L'item de la Liste des comptes. Un Result par query (ADR 0012) : la forme
// d'une ligne de liste est la sienne, pas celle de BankAccountResult — les
// deux coïncident presque aujourd'hui, elles divergeront sans se le signaler.
// L'enveloppe (Page, ADR 0027) est du socle, pas d'ici.
public sealed record BankAccountSummaryResult(
    Guid Id,
    string Iban,
    decimal Balance,
    string Currency,
    bool IsClosed,
    DateTimeOffset OpenedAt
);
