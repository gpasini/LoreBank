namespace LoreBank.Bank.Application.Queries.ListBankAccounts;

// La liste des comptes, triée par IBAN. Un Result par query (ADR 0012) : la
// forme d'une ligne de liste est la sienne, pas celle de BankAccountResult —
// les deux coïncident aujourd'hui, elles divergeront sans se le signaler.
public sealed record BankAccountsResult(IReadOnlyList<BankAccountSummaryResult> Accounts);

public sealed record BankAccountSummaryResult(
    Guid Id,
    string Iban,
    decimal Balance,
    string Currency,
    bool IsClosed
);
