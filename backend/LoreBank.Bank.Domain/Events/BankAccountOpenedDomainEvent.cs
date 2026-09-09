using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Events;

public sealed record BankAccountOpenedDomainEvent(
    BankAccountId AccountId,
    Iban Iban,
    Actor OpenedBy,
    DateTimeOffset OpenedAt
) : IDomainEvent;
