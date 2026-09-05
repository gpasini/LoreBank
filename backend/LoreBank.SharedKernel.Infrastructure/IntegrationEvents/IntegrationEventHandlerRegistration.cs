using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// Le fait « ce handler, de ce module, consomme cet event », déclaré au
// conteneur plutôt que re-dérivé au dispatch : c'est le nom du module qui
// désigne l'inbox (et le DbContext) du consommateur, et l'event type qui
// permet de désérialiser le payload. Valide à la construction — une
// registration mal formée casse à la composition, pas au premier event.
public sealed class IntegrationEventHandlerRegistration
{
    public IntegrationEventHandlerRegistration(
        Type handlerType,
        Type eventType,
        string moduleName
    )
    {
        var handlerInterface = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

        if (!handlerInterface.IsAssignableFrom(handlerType)) {
            throw new InvalidOperationException(
                $"{handlerType.Name} n'implémente pas IIntegrationEventHandler<{eventType.Name}> : "
                + "la registration ne correspond à aucun handler résolvable."
            );
        }

        HandlerType = handlerType;
        EventType = eventType;
        ModuleName = moduleName;
        Discriminant = IntegrationEventDiscriminant.Of(eventType);
    }

    public Type HandlerType { get; }

    public Type EventType { get; }

    // Le module consommateur — celui dont l'inbox journalise le traitement,
    // et dont le DbContext porte la transaction du handler.
    public string ModuleName { get; }

    public string Discriminant { get; }
}
