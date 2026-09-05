namespace LoreBank.SharedKernel.Contracts;

// La réaction d'un module consommateur à l'integration event d'un autre.
// Contrairement à IDomainEventHandler (dans la transaction de la commande),
// un handler d'integration event s'exécute plus tard, dans sa propre
// transaction, avec une livraison at-least-once : l'inbox du socle le rend
// idempotent — il peut malgré tout être invoqué deux fois si un crash tombe
// entre son commit et le marquage de l'outbox, l'idempotence métier reste
// une vertu.
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(
        TEvent integrationEvent,
        CancellationToken cancellationToken
    );
}
