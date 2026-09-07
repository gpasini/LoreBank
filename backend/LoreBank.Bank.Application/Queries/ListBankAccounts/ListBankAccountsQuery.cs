using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Queries.ListBankAccounts;

public sealed record ListBankAccountsQuery : IQuery<BankAccountsResult>;
