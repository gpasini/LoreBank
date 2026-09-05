using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Events;

public sealed record MoneyWithdrawnDomainEvent(
    BankAccountId AccountId,
    Money Amount,
    Money NewBalance
) : IDomainEvent;
