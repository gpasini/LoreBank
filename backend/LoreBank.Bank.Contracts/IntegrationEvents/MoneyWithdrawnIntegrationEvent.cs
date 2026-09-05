using LoreBank.SharedKernel.Contracts;

namespace LoreBank.Bank.Contracts.IntegrationEvents;

[IntegrationEvent("bank.money-withdrawn")]
public sealed record MoneyWithdrawnIntegrationEvent(
    Guid AccountId,
    decimal Amount,
    string Currency
) : IIntegrationEvent;
