using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Exceptions;

public sealed class InsufficientBalanceException(
    Money balance,
    Money requested
) : DomainException(
    new() {
        ["balance"] = balance.Amount,
        ["requested"] = requested.Amount,
        ["currency"] = balance.Currency,
    }
);
