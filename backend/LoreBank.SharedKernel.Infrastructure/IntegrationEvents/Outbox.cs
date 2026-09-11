using LoreBank.SharedKernel.Infrastructure.Persistence;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// L'outbox d'un module : le seul endroit qui sache comment une ligne est
// faite. Le DDL, les colonnes et les sept gestes qui les touchent vivent
// ici — ajouter une colonne ne touche que ce fichier, alors que le bail, le
// traceparent puis la ressource de Signal en avaient touché cinq chacun.
//
// Le store se construit sur un DbContext déjà résolu : c'est l'appelant —
// par Outboxes — qui décide dans quel scope, donc dans quelle transaction,
// le geste s'exécute. La connexion est empruntée à ce DbContext par
// ModuleSql, jamais ouverte en propre.
//
// Ce qu'il ne fait pas : décider. Le backoff, le seuil de poison et la
// rétention sont la politique du processor (ADR 0021) ; le store écrit les
// valeurs qu'on lui donne.
internal sealed class Outbox(ModuleDbContext dbContext)
{
    // Ce qu'une publication dépose : l'identité de la ligne, le fait
    // sérialisé, la trace d'origine, et la ressource que l'event nomme s'il
    // signale les clients — hors payload, en deux colonnes (ADR 0026).
    internal sealed record Entry(
        Guid Id,
        string Discriminant,
        string Payload,
        string? TraceParent,
        string? ResourceKind,
        Guid? ResourceId
    );

    // Ce qu'une passe de livraison s'approprie.
    internal sealed record Reserved(
        Guid Id,
        string Discriminant,
        string Payload,
        int Attempts,
        string? TraceParent
    );

    // Ce que le suiveur de Signal relit : une ligne livrée qui nomme une
    // ressource.
    internal sealed record Dispatched(
        Guid Id,
        string Discriminant,
        string ResourceKind,
        Guid ResourceId,
        DateTimeOffset OccurredAt,
        DateTime DispatchedAt
    );

    // La ligne entière, pour observer — la production n'en lit jamais autant,
    // le harnais si : c'est ce qui évite que les noms de colonnes soient
    // réécrits dans les sondes.
    internal sealed record Row(
        Guid Id,
        string Discriminant,
        string Payload,
        int Attempts,
        bool Dispatched,
        bool Poisoned,
        string? LastError,
        bool Reserved,
        string? TraceParent,
        string? ResourceKind,
        Guid? ResourceId
    );

    // Le DDL, idempotent et cumulatif (ADR 0021) : le CREATE TABLE porte la
    // forme complète pour une base neuve, et chaque colonne ou index venu
    // après rattrape les bases existantes par son propre IF NOT EXISTS —
    // jamais en réécrivant le CREATE seul. Pas de timeline de migration :
    // c'est une table du socle, pas du module.
    internal static Task EnsureTableAsync(
        ModuleDbContext dbContext,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  CREATE TABLE IF NOT EXISTS {TableOf(dbContext)} (
                      id uuid NOT NULL PRIMARY KEY,
                      discriminant character varying(200) NOT NULL,
                      payload jsonb NOT NULL,
                      occurred_at timestamp with time zone NOT NULL,
                      next_attempt_at timestamp with time zone NOT NULL,
                      attempts integer NOT NULL,
                      dispatched_at timestamp with time zone,
                      poisoned_at timestamp with time zone,
                      last_error text,
                      reserved_until timestamp with time zone,
                      trace_parent character varying(55),
                      resource_kind character varying(100),
                      resource_id uuid
                  );
                  ALTER TABLE {TableOf(dbContext)}
                      ADD COLUMN IF NOT EXISTS reserved_until timestamp with time zone;
                  ALTER TABLE {TableOf(dbContext)}
                      ADD COLUMN IF NOT EXISTS trace_parent character varying(55);
                  ALTER TABLE {TableOf(dbContext)}
                      ADD COLUMN IF NOT EXISTS resource_kind character varying(100);
                  ALTER TABLE {TableOf(dbContext)}
                      ADD COLUMN IF NOT EXISTS resource_id uuid;
                  CREATE INDEX IF NOT EXISTS ix___outbox_pending
                      ON {TableOf(dbContext)} (next_attempt_at)
                      WHERE dispatched_at IS NULL AND poisoned_at IS NULL;
                  CREATE INDEX IF NOT EXISTS ix___outbox_dispatched
                      ON {TableOf(dbContext)} (dispatched_at)
                      WHERE dispatched_at IS NOT NULL;
                  """,
            parameters: new Dictionary<string, object>(),
            cancellationToken: cancellationToken
        );

    // Le nom de la table, pour les rares gestes qui ne passent pas par un
    // verbe : le harnais qui simule un crash ou une autre instance.
    internal static string TableOf(ModuleDbContext dbContext) => $"{dbContext.Schema}.__outbox";

    internal Task InsertAsync(
        Entry entry,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  INSERT INTO {Table}
                      (id, discriminant, payload, occurred_at, next_attempt_at, attempts, trace_parent,
                       resource_kind, resource_id)
                  VALUES (@id, @discriminant, CAST(@payload AS jsonb), now(), now(), 0, @traceParent,
                          @resourceKind, @resourceId)
                  """,
            parameters: new Dictionary<string, object> {
                ["id"] = entry.Id,
                ["discriminant"] = entry.Discriminant,
                ["payload"] = entry.Payload,
                ["traceParent"] = (object?) entry.TraceParent ?? DBNull.Value,
                ["resourceKind"] = (object?) entry.ResourceKind ?? DBNull.Value,
                ["resourceId"] = (object?) entry.ResourceId ?? DBNull.Value,
            },
            cancellationToken: cancellationToken
        );

    // La réservation du lot : les lignes éligibles — en attente,
    // ré-éligibles au backoff, sans bail en cours — reçoivent un bail en une
    // seule instruction, donc une seule transaction, courte : SKIP LOCKED
    // écarte celles qu'une passe simultanée est en train de réserver, et le
    // bail posé les écarte des passes suivantes. RETURNING ne garantit pas
    // l'ordre, il est rétabli en mémoire.
    internal Task<IReadOnlyList<Reserved>> ReserveAsync(
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  UPDATE {Table}
                  SET reserved_until = now() + make_interval(secs => @leaseSeconds)
                  WHERE id IN (
                      SELECT id FROM {Table}
                      WHERE dispatched_at IS NULL AND poisoned_at IS NULL AND next_attempt_at <= now()
                        AND (reserved_until IS NULL OR reserved_until < now())
                      ORDER BY occurred_at, id
                      LIMIT @batchSize
                      FOR UPDATE SKIP LOCKED
                  )
                  RETURNING id, discriminant, payload, attempts, occurred_at, trace_parent
                  """,
            parameters: new Dictionary<string, object> {
                ["leaseSeconds"] = lease.TotalSeconds,
                ["batchSize"] = batchSize,
            },
            execute: async (
                command,
                token
            ) =>
            {
                var rows = new List<(DateTime OccurredAt, Reserved Row)>();

                await using var reader = await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token)) {
                    rows.Add((
                        reader.GetDateTime(4),
                        new Reserved(
                            Id: reader.GetGuid(0),
                            Discriminant: reader.GetString(1),
                            Payload: reader.GetString(2),
                            Attempts: reader.GetInt32(3),
                            TraceParent: reader.IsDBNull(5) ? null : reader.GetString(5)
                        )
                    ));
                }

                return (IReadOnlyList<Reserved>) rows
                    .OrderBy(entry => entry.OccurredAt)
                    .ThenBy(entry => entry.Row.Id)
                    .Select(entry => entry.Row)
                    .ToList();
            },
            cancellationToken: cancellationToken
        );

    internal Task MarkDispatchedAsync(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  UPDATE {Table}
                  SET dispatched_at = now(), reserved_until = NULL
                  WHERE id = @id
                  """,
            parameters: new Dictionary<string, object> {
                ["id"] = id,
            },
            cancellationToken: cancellationToken
        );

    internal Task RecordFailureAsync(
        Guid id,
        int attempts,
        string lastError,
        double delaySeconds,
        bool poisoned,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  UPDATE {Table}
                  SET attempts = @attempts,
                      last_error = @lastError,
                      next_attempt_at = now() + make_interval(secs => @delaySeconds),
                      poisoned_at = CASE WHEN @poisoned THEN now() END,
                      reserved_until = NULL
                  WHERE id = @id
                  """,
            parameters: new Dictionary<string, object> {
                ["id"] = id,
                ["attempts"] = attempts,
                ["lastError"] = lastError,
                ["delaySeconds"] = delaySeconds,
                ["poisoned"] = poisoned,
            },
            cancellationToken: cancellationToken
        );

    // Le comptage d'une outbox, bon marché tant que la Rétention tient la
    // table petite.
    internal Task<OutboxDepth> MeasureAsync(CancellationToken cancellationToken) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  SELECT count(*) FILTER (WHERE dispatched_at IS NULL AND poisoned_at IS NULL),
                         count(*) FILTER (WHERE poisoned_at IS NOT NULL)
                  FROM {Table}
                  """,
            parameters: new Dictionary<string, object>(),
            execute: async (
                command,
                token
            ) =>
            {
                await using var reader = await command.ExecuteReaderAsync(token);
                await reader.ReadAsync(token);

                return new OutboxDepth(
                    Pending: reader.GetInt64(0),
                    Poisoned: reader.GetInt64(1)
                );
            },
            cancellationToken: cancellationToken
        );

    // La Rétention (ADR 0021) : les lignes livrées plus vieilles que la
    // rétention disparaissent — jamais une ligne en attente (elle n'a pas de
    // dispatched_at), jamais une ligne poison (elle reste pour un humain).
    // Idempotente : deux instances qui purgent en même temps ne se gênent pas.
    internal Task<int> PurgeAsync(
        int retentionDays,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  DELETE FROM {Table}
                  WHERE dispatched_at IS NOT NULL
                    AND dispatched_at < now() - make_interval(days => @retentionDays)
                  """,
            parameters: new Dictionary<string, object> {
                ["retentionDays"] = retentionDays,
            },
            cancellationToken: cancellationToken
        );

    // La relecture du suiveur de Signal (ADR 0026) : ce qui a été marqué
    // livré depuis le curseur, et qui nomme une ressource — le Signal est
    // opt-in par l'event.
    internal Task<IReadOnlyList<Dispatched>> ReadDispatchedSinceAsync(
        DateTime since,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  SELECT id, discriminant, resource_kind, resource_id, occurred_at, dispatched_at
                  FROM {Table}
                  WHERE dispatched_at > @since AND resource_kind IS NOT NULL
                  ORDER BY dispatched_at, id
                  """,
            parameters: new Dictionary<string, object> {
                ["since"] = since,
            },
            execute: async (
                command,
                token
            ) =>
            {
                var rows = new List<Dispatched>();

                await using var reader = await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token)) {
                    rows.Add(new Dispatched(
                            Id: reader.GetGuid(0),
                            Discriminant: reader.GetString(1),
                            ResourceKind: reader.GetString(2),
                            ResourceId: reader.GetGuid(3),
                            OccurredAt: reader.GetFieldValue<DateTimeOffset>(4),
                            DispatchedAt: reader.GetDateTime(5)
                        )
                    );
                }

                return (IReadOnlyList<Dispatched>) rows;
            },
            cancellationToken: cancellationToken
        );

    // L'horloge qui tamponne dispatched_at : celle de la base, jamais celle
    // du process — c'est à elle que le curseur du suiveur se compare.
    internal Task<DateTime> NowAsync(CancellationToken cancellationToken) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: "SELECT now()",
            parameters: new Dictionary<string, object>(),
            execute: async (
                command,
                token
            ) => (DateTime) (await command.ExecuteScalarAsync(token))!,
            cancellationToken: cancellationToken
        );

    internal Task<IReadOnlyList<Row>> ReadAllAsync(CancellationToken cancellationToken) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  SELECT id, discriminant, payload, attempts, dispatched_at IS NOT NULL,
                         poisoned_at IS NOT NULL, last_error, reserved_until IS NOT NULL,
                         trace_parent, resource_kind, resource_id
                  FROM {Table}
                  ORDER BY occurred_at, id
                  """,
            parameters: new Dictionary<string, object>(),
            execute: async (
                command,
                token
            ) =>
            {
                var rows = new List<Row>();

                await using var reader = await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token)) {
                    rows.Add(new Row(
                            Id: reader.GetGuid(0),
                            Discriminant: reader.GetString(1),
                            Payload: reader.GetString(2),
                            Attempts: reader.GetInt32(3),
                            Dispatched: reader.GetBoolean(4),
                            Poisoned: reader.GetBoolean(5),
                            LastError: reader.IsDBNull(6) ? null : reader.GetString(6),
                            Reserved: reader.GetBoolean(7),
                            TraceParent: reader.IsDBNull(8) ? null : reader.GetString(8),
                            ResourceKind: reader.IsDBNull(9) ? null : reader.GetString(9),
                            ResourceId: reader.IsDBNull(10) ? null : reader.GetGuid(10)
                        )
                    );
                }

                return (IReadOnlyList<Row>) rows;
            },
            cancellationToken: cancellationToken
        );

    private string Table => TableOf(dbContext);
}
