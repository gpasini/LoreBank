namespace LoreBank.SharedKernel.Contracts;

// Le port qu'un domain event handler du module publieur consomme pour faire
// sortir un fait de ses frontières. L'implémentation du socle écrit la ligne
// d'outbox par la connexion du DbContext du module : appelé dans la
// transaction de la commande (le dispatch des domain events y a lieu),
// l'event part avec elle ou pas du tout.
public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    );
}
