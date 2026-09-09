using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Queries.ListBankAccounts;

// La Liste des comptes (ADR 0027) : recherche sur l'IBAN, deux filtres
// facettés — devise et clôture — multi-valeurs, comme toute ListQuery. Le
// tri (IBAN) est celui du reader, pas un paramètre.
public sealed record ListBankAccountsQuery : ListQuery<BankAccountSummaryResult>
{
    public IReadOnlyList<string>? Currency { get; init; }

    public IReadOnlyList<bool>? IsClosed { get; init; }
}
