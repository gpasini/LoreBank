using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Queries.GetBankAccountById;

public sealed record GetBankAccountByIdQuery(Guid AccountId) : IQuery<BankAccountResult>;
