using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Bank.Domain.Events;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.Bank.Domain.EventHandlers;

public sealed class MoneyWithdrawnDomainEventHandler(IIntegrationEventPublisher integrationEventPublisher)
    : IDomainEventHandler<MoneyWithdrawnDomainEvent>
{
    public Task HandleAsync(
        MoneyWithdrawnDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => integrationEventPublisher.PublishAsync(
        integrationEvent: new MoneyWithdrawnIntegrationEvent(
            AccountId: domainEvent.AccountId.Value,
            Amount: domainEvent.Amount.Amount,
            Currency: domainEvent.Amount.Currency
        ),
        cancellationToken: cancellationToken
    );
}
