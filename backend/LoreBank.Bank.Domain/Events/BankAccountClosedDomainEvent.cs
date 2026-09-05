using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.Bank.Domain.Events;

public sealed record BankAccountClosedDomainEvent(BankAccountId AccountId) : IDomainEvent;
