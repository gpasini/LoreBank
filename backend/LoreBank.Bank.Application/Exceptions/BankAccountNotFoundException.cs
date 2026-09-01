using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.Bank.Application.Exceptions;

public sealed class BankAccountNotFoundException(BankAccountId accountId) : NotFoundException(
    new() { ["accountId"] = accountId.Value }
);
