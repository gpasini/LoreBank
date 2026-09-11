using System.Diagnostics;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// L'implémentation du port : une ligne dans l'outbox du module publieur,
// désigné par le premier segment du discriminant. Le geste passe par la
// porte « scope de l'appelant » d'IntegrationEventStores — celui de la commande — donc la
// ligne part avec la transaction de la commande ou pas du tout.
//
// Ce que le publisher décide, et que le store se contente d'écrire :
// l'identité de la ligne, le traceparent W3C de l'Activity courante — celle
// que le hosting pose sur la requête — pour que le traitement du
// consommateur soit un enfant de la même trace (ADR 0025), nul hors activité
// (migration de données, test) sans erreur ; et la ressource que l'event
// nomme s'il signale les clients (ISignalsClients, ADR 0026), validée ici, à
// la frontière.
public sealed class OutboxPublisher(
    IntegrationEventStores stores,
    IServiceProvider serviceProvider
) : IIntegrationEventPublisher
{
    public Task PublishAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        var discriminant = IntegrationEventDiscriminant.Of(integrationEvent.GetType());
        var resource = integrationEvent is ISignalsClients signals
            ? SignalResource.Of(
                kind: signals.ResourceKind,
                id: signals.ResourceId
            )
            : null;

        return stores.InCallerScopeAsync(
            callerScope: serviceProvider,
            moduleName: IntegrationEventDiscriminant.ModuleOf(discriminant),
            purpose: "un integration event se publie depuis l'outbox de son module, désigné par le premier "
            + $"segment du discriminant « {discriminant} »",
            action: outbox => outbox.InsertAsync(
                entry: new Outbox.Entry(
                    Id: Guid.NewGuid(),
                    Discriminant: discriminant,
                    Payload: IntegrationEventJson.Serialize(integrationEvent),
                    TraceParent: Activity.Current?.Id,
                    ResourceKind: resource?.Kind,
                    ResourceId: resource?.Id
                ),
                cancellationToken: cancellationToken
            )
        );
    }
}
