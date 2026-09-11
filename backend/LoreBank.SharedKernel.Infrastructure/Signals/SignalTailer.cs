using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Modules;

namespace LoreBank.SharedKernel.Infrastructure.Signals;

// Le suiveur des outbox (ADR 0026) : la ligne marquée livrée est la
// notification. À chaque passe — celle de l'OutboxDispatcher, après la
// livraison, ou celle qu'un test pilote — chaque instance de l'hôte relit
// les lignes livrées depuis son curseur, un par module, et pousse au hub un
// Signal par ligne qui porte une ressource. Ce qu'une instance a livré,
// toutes le voient dans la table : le multi-instance (ADR 0021) sans
// connexion longue ni nouvelle pièce. La lecture passe par le store, porte
// « scope neuf » : ce qui reste ici est le curseur, rien du SQL.
//
// Le curseur d'un module naît au premier passage, à l'Instant de la base :
// ce qui a été livré avant ne regarde pas cette instance, ses clients n'y
// étaient pas connectés. Un marquage « livrée » est commité par une autre
// instance avec son propre now(), parfois après qu'un passage a lu :
// chaque lecture recouvre les dernières secondes, et une mémoire courte
// des ids déjà signalés absorbe le recouvrement. Piloté depuis une seule
// boucle, comme le processor : pas synchronisé.
public sealed class SignalTailer(
    IEnumerable<IHostModule> modules,
    IntegrationEventStores stores,
    SignalHub hub
)
{
    internal readonly static TimeSpan Overlap = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, Cursor> _cursors = new(StringComparer.OrdinalIgnoreCase);

    public async Task TailAsync(CancellationToken cancellationToken)
    {
        foreach (var module in modules) {
            if (!_cursors.TryGetValue(
                    key: module.ModuleName,
                    value: out var cursor
                )) {
                _cursors[module.ModuleName] = new Cursor(await stores.InOwnScopeAsync(
                        module: module,
                        action: (
                            outbox,
                            _
                        ) => outbox.NowAsync(cancellationToken)
                    )
                );

                continue;
            }

            var rows = await stores.InOwnScopeAsync(
                module: module,
                action: (
                    outbox,
                    _
                ) => outbox.ReadDispatchedSinceAsync(
                    since: cursor.Since,
                    cancellationToken: cancellationToken
                )
            );

            foreach (var row in rows) {
                if (!cursor.Remember(
                        id: row.Id,
                        dispatchedAt: row.DispatchedAt
                    )) {
                    continue;
                }

                hub.Publish(new Signal(
                        Discriminant: row.Discriminant,
                        ResourceKind: row.ResourceKind,
                        ResourceId: row.ResourceId,
                        OccurredAt: row.OccurredAt
                    )
                );
            }

            cursor.Forget();
        }
    }

    // La position d'un module : le dernier marquage vu, la fenêtre de
    // recouvrement en deçà, et les ids déjà signalés dans cette fenêtre.
    private sealed class Cursor(DateTime position)
    {
        private readonly Dictionary<Guid, DateTime> _seen = new();

        private DateTime _position = position;

        public DateTime Since => _position - Overlap;

        public bool Remember(
            Guid id,
            DateTime dispatchedAt
        )
        {
            if (!_seen.TryAdd(
                    key: id,
                    value: dispatchedAt
                )) {
                return false;
            }

            if (dispatchedAt > _position) {
                _position = dispatchedAt;
            }

            return true;
        }

        public void Forget()
        {
            foreach (var id in _seen.Where(entry => entry.Value <= Since).Select(entry => entry.Key).ToList()) {
                _seen.Remove(id);
            }
        }
    }
}
