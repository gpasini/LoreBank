using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.Bank.Domain.Exceptions;

public sealed class AccountClosedException(BankAccountId accountId) : DomainException(
    new() { ["accountId"] = accountId.Value }
);
