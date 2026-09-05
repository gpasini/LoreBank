using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Bank.Domain.Events;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.Bank.Domain.EventHandlers;

// Le pont vers le langage publié : traduit le fait interne (VO) en son jumeau
// public (primitives) et le confie à l'outbox. Il tourne dans la transaction
// de la commande — l'event publié part avec elle ou pas du tout (ADR 0014).
// C'est ce handler qui rend la publication opt-in : seuls les faits qu'un
// handler mappe sortent du module.
public sealed class MoneyDepositedDomainEventHandler(IIntegrationEventPublisher integrationEventPublisher)
    : IDomainEventHandler<MoneyDepositedDomainEvent>
{
    public Task HandleAsync(
        MoneyDepositedDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => integrationEventPublisher.PublishAsync(
        integrationEvent: new MoneyDepositedIntegrationEvent(
            AccountId: domainEvent.AccountId.Value,
            Amount: domainEvent.Amount.Amount,
            Currency: domainEvent.Amount.Currency
        ),
        cancellationToken: cancellationToken
    );
}
