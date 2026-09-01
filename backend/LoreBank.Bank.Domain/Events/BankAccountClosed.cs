using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.Bank.Domain.Events;

public sealed record BankAccountClosed(BankAccountId AccountId) : IDomainEvent;
