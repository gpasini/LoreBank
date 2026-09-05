using LoreBank.SharedKernel.Infrastructure.Persistence;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// L'outbox et l'inbox d'un module naissent paresseusement au passage de
// ModuleMigrator, comme le journal des data migrations : des tables du socle,
// pas du module — aucune migration EF à générer ni à recopier au clonage, et
// tout module les a d'office (un module qui ne publie rien a une outbox
// vide, c'est tout). Le démarrage de l'API, lui, ne crée toujours rien
// (ADR 0006).
public static class IntegrationEventTables
{
    public static Task EnsureAsync(
        ModuleDbContext dbContext,
        CancellationToken cancellationToken
    ) =>
        OutboxSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  CREATE SCHEMA IF NOT EXISTS {dbContext.Schema};
                  CREATE TABLE IF NOT EXISTS {OutboxTable(dbContext)} (
                      id uuid NOT NULL PRIMARY KEY,
                      discriminant character varying(200) NOT NULL,
                      payload jsonb NOT NULL,
                      occurred_at timestamp with time zone NOT NULL,
                      next_attempt_at timestamp with time zone NOT NULL,
                      attempts integer NOT NULL,
                      dispatched_at timestamp with time zone,
                      poisoned_at timestamp with time zone,
                      last_error text
                  );
                  CREATE INDEX IF NOT EXISTS ix___outbox_pending
                      ON {OutboxTable(dbContext)} (next_attempt_at)
                      WHERE dispatched_at IS NULL AND poisoned_at IS NULL;
                  CREATE TABLE IF NOT EXISTS {InboxTable(dbContext)} (
                      event_id uuid NOT NULL,
                      handler character varying(300) NOT NULL,
                      handled_at timestamp with time zone NOT NULL,
                      PRIMARY KEY (event_id, handler)
                  );
                  """,
            parameters: new Dictionary<string, object>(),
            cancellationToken: cancellationToken
        );

    internal static string OutboxTable(ModuleDbContext dbContext) => $"{dbContext.Schema}.__outbox";

    internal static string InboxTable(ModuleDbContext dbContext) => $"{dbContext.Schema}.__inbox";
}
