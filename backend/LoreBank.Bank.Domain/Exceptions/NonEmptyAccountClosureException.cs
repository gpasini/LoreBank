using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Exceptions;

public sealed class NonEmptyAccountClosureException(
    BankAccountId accountId,
    Money balance
) : DomainException(
    new() {
        ["accountId"] = accountId.Value,
        ["balance"] = balance.Amount,
        ["currency"] = balance.Currency,
    }
);
