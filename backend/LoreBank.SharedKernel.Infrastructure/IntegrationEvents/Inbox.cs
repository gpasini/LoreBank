using LoreBank.SharedKernel.Infrastructure.Persistence;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// L'inbox d'un module consommateur : le journal des integration events
// traités, écrit dans la transaction du handler — c'est elle qui rend la
// livraison at-least-once idempotente (ADR 0014). La clé est la paire
// (event, handler) : deux consommateurs du même event se journalisent
// séparément.
//
// Pas d'accesseur jumeau d'Outboxes : le processor possède déjà le scope où
// l'inbox s'écrit — celui de son handler — et y résout le DbContext du
// module consommateur. Le store se construit dessus.
internal sealed class Inbox(ModuleDbContext dbContext)
{
    // Le DDL, idempotent et cumulatif comme celui de l'outbox (ADR 0021).
    internal static Task EnsureTableAsync(
        ModuleDbContext dbContext,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  CREATE TABLE IF NOT EXISTS {TableOf(dbContext)} (
                      event_id uuid NOT NULL,
                      handler character varying(300) NOT NULL,
                      handled_at timestamp with time zone NOT NULL,
                      PRIMARY KEY (event_id, handler)
                  );
                  CREATE INDEX IF NOT EXISTS ix___inbox_handled
                      ON {TableOf(dbContext)} (handled_at);
                  """,
            parameters: new Dictionary<string, object>(),
            cancellationToken: cancellationToken
        );

    internal static string TableOf(ModuleDbContext dbContext) => $"{dbContext.Schema}.__inbox";

    internal Task<bool> IsHandledAsync(
        Guid eventId,
        string handler,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  SELECT count(*) FROM {Table}
                  WHERE event_id = @eventId AND handler = @handler
                  """,
            parameters: KeyOf(
                eventId: eventId,
                handler: handler
            ),
            execute: async (
                command,
                token
            ) => (long) (await command.ExecuteScalarAsync(token))! > 0,
            cancellationToken: cancellationToken
        );

    internal Task MarkHandledAsync(
        Guid eventId,
        string handler,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  INSERT INTO {Table} (event_id, handler, handled_at)
                  VALUES (@eventId, @handler, now())
                  """,
            parameters: KeyOf(
                eventId: eventId,
                handler: handler
            ),
            cancellationToken: cancellationToken
        );

    // La Rétention (ADR 0021) : une trace d'inbox n'a de sens que tant que
    // son outbox peut rejouer, donc la même durée que l'outbox.
    internal Task<int> PurgeAsync(
        int retentionDays,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  DELETE FROM {Table}
                  WHERE handled_at < now() - make_interval(days => @retentionDays)
                  """,
            parameters: new Dictionary<string, object> {
                ["retentionDays"] = retentionDays,
            },
            cancellationToken: cancellationToken
        );

    private static Dictionary<string, object> KeyOf(
        Guid eventId,
        string handler
    ) =>
        new() {
            ["eventId"] = eventId,
            ["handler"] = handler,
        };

    private string Table => TableOf(dbContext);
}
