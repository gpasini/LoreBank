using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Signals;

// Le suiveur des outbox (ADR 0026) : la ligne marquée livrée est la
// notification. À chaque passe — celle de l'OutboxDispatcher, après la
// livraison, ou celle qu'un test pilote — chaque instance de l'hôte relit
// les lignes livrées depuis son curseur, un par module, et pousse au hub un
// Signal par ligne qui porte une ressource. Ce qu'une instance a livré,
// toutes le voient dans la table : le multi-instance (ADR 0021) sans
// connexion longue ni nouvelle pièce.
//
// Le curseur d'un module naît au premier passage, à l'Instant de la base :
// ce qui a été livré avant ne regarde pas cette instance, ses clients n'y
// étaient pas connectés. Un marquage « livrée » est commité par une autre
// instance avec son propre now(), parfois après qu'un passage a lu :
// chaque lecture recouvre les dernières secondes, et une mémoire courte
// des ids déjà signalés absorbe le recouvrement. Piloté depuis une seule
// boucle, comme le processor : pas synchronisé.
public sealed class SignalTailer(
    IServiceProvider serviceProvider,
    IEnumerable<IHostModule> modules,
    SignalHub hub
)
{
    internal static readonly TimeSpan Overlap = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, Cursor> _cursors = new(StringComparer.OrdinalIgnoreCase);

    public async Task TailAsync(CancellationToken cancellationToken)
    {
        foreach (var module in modules) {
            await using var scope = serviceProvider.CreateAsyncScope();

            var dbContext = ModuleDbContexts.Resolve(
                services: scope.ServiceProvider,
                module: module
            );

            if (!_cursors.TryGetValue(
                    key: module.ModuleName,
                    value: out var cursor
                )) {
                _cursors[module.ModuleName] = new Cursor(await NowAsync(
                        dbContext: dbContext,
                        cancellationToken: cancellationToken
                    )
                );

                continue;
            }

            var rows = await ReadDispatchedSinceAsync(
                dbContext: dbContext,
                since: cursor.Since,
                cancellationToken: cancellationToken
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

    private static Task<DateTime> NowAsync(
        ModuleDbContext dbContext,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: "SELECT now()",
            parameters: new Dictionary<string, object>(),
            execute: async (
                command,
                token
            ) => (DateTime)(await command.ExecuteScalarAsync(token))!,
            cancellationToken: cancellationToken
        );

    private static Task<IReadOnlyList<Row>> ReadDispatchedSinceAsync(
        ModuleDbContext dbContext,
        DateTime since,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  SELECT id, discriminant, resource_kind, resource_id, occurred_at, dispatched_at
                  FROM {IntegrationEventTables.OutboxTable(dbContext)}
                  WHERE dispatched_at > @since AND resource_kind IS NOT NULL
                  ORDER BY dispatched_at, id
                  """,
            parameters: new Dictionary<string, object> {
                ["since"] = since,
            },
            execute: async (
                command,
                token
            ) => {
                var rows = new List<Row>();

                await using var reader = await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token)) {
                    rows.Add(new Row(
                            Id: reader.GetGuid(0),
                            Discriminant: reader.GetString(1),
                            ResourceKind: reader.GetString(2),
                            ResourceId: reader.GetGuid(3),
                            OccurredAt: reader.GetFieldValue<DateTimeOffset>(4),
                            DispatchedAt: reader.GetDateTime(5)
                        )
                    );
                }

                return (IReadOnlyList<Row>)rows;
            },
            cancellationToken: cancellationToken
        );

    private sealed record Row(
        Guid Id,
        string Discriminant,
        string ResourceKind,
        Guid ResourceId,
        DateTimeOffset OccurredAt,
        DateTime DispatchedAt
    );

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
