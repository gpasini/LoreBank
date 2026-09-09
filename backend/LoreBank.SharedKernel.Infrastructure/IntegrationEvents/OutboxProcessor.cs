using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Transactions;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// Une passe de livraison, séparée du hosted service qui la cadence pour que
// les tests la pilotent déterministiquement. Livraison at-least-once : chaque
// handler s'exécute dans son propre scope DI et son propre TransactionScope,
// ligne d'inbox incluse — un handler qui échoue n'annule ni les autres ni la
// commande d'origine, il remet la ligne d'outbox en attente avec backoff,
// jusqu'au marquage poison. Le marquage « dispatché » de l'outbox est hors de
// ces transactions (schéma du publieur, connexion distincte — la joindre
// ferait escalader en distribué) : un crash entre les deux rejoue l'event, et
// c'est l'inbox qui rend le rejeu inoffensif. Plusieurs instances de l'hôte
// dépilent la même outbox par Réservation (ADR 0021) : la lecture du lot est
// une appropriation à bail, en une requête courte — FOR UPDATE SKIP LOCKED
// contre une passe simultanée, reserved_until contre celles qui suivent —
// et chaque marquage rend la réservation.
public sealed class OutboxProcessor(
    IServiceProvider serviceProvider,
    IEnumerable<IHostModule> modules,
    IEnumerable<IntegrationEventHandlerRegistration> registrations,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger
)
{
    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        foreach (var module in modules) {
            var rows = await ReserveBatchAsync(
                module: module,
                cancellationToken: cancellationToken
            );

            foreach (var row in rows) {
                await ProcessRowAsync(
                    publisherModule: module,
                    row: row,
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    // La Rétention (ADR 0021) : les lignes livrées et les traces d'inbox
    // plus vieilles que la rétention disparaissent — jamais une ligne en
    // attente (elle n'a pas de dispatched_at), jamais une ligne poison (elle
    // reste pour un humain). Idempotente : deux instances qui purgent en
    // même temps ne se gênent pas.
    public async Task PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        foreach (var module in modules) {
            await using var scope = serviceProvider.CreateAsyncScope();

            var dbContext = ModuleDbContexts.Resolve(
                services: scope.ServiceProvider,
                module: module
            );

            await ModuleSql.ExecuteNonQueryAsync(
                dbContext: dbContext,
                sql: $"""
                      DELETE FROM {IntegrationEventTables.OutboxTable(dbContext)}
                      WHERE dispatched_at IS NOT NULL
                        AND dispatched_at < now() - make_interval(days => @retentionDays);
                      DELETE FROM {IntegrationEventTables.InboxTable(dbContext)}
                      WHERE handled_at < now() - make_interval(days => @retentionDays);
                      """,
                parameters: new Dictionary<string, object> {
                    ["retentionDays"] = options.Value.RetentionDays,
                },
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task ProcessRowAsync(
        IHostModule publisherModule,
        OutboxRow row,
        CancellationToken cancellationToken
    )
    {
        Exception? firstFailure = null;

        foreach (var registration in registrations.Where(candidate => candidate.Discriminant == row.Discriminant)) {
            try {
                await HandleAsync(
                    registration: registration,
                    row: row,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception exception) {
                firstFailure ??= exception;
                logger.LogWarning(
                    exception: exception,
                    message: "Le handler {Handler} a échoué sur l'event {Discriminant} {EventId} (tentative {Attempt}).",
                    registration.HandlerType.Name,
                    row.Discriminant,
                    row.Id,
                    row.Attempts + 1
                );
            }
        }

        if (firstFailure is null) {
            await MarkDispatchedAsync(
                publisherModule: publisherModule,
                row: row,
                cancellationToken: cancellationToken
            );
        }
        else {
            await RecordFailureAsync(
                publisherModule: publisherModule,
                row: row,
                failure: firstFailure,
                cancellationToken: cancellationToken
            );
        }
    }

    // La transaction du handler : scope DI neuf, TransactionScope au niveau
    // du TransactionBehavior (ReadCommitted), inbox lue et écrite par la
    // connexion du DbContext du module consommateur — le même connecteur que
    // les écritures du handler, donc tout commite ou rien.
    private async Task HandleAsync(
        IntegrationEventHandlerRegistration registration,
        OutboxRow row,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        using var transaction = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            transactionOptions: new TransactionOptions {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TransactionManager.DefaultTimeout,
            },
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled
        );

        var dbContext = ConsumerDbContextFor(
            registration: registration,
            scope: scope.ServiceProvider
        );

        if (await IsAlreadyHandledAsync(
                dbContext: dbContext,
                registration: registration,
                row: row,
                cancellationToken: cancellationToken
            )) {
            transaction.Complete();

            return;
        }

        await InvokeHandlerAsync(
            registration: registration,
            scope: scope.ServiceProvider,
            integrationEvent: IntegrationEventJson.Deserialize(
                payload: row.Payload,
                eventType: registration.EventType
            ),
            cancellationToken: cancellationToken
        );

        await ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  INSERT INTO {IntegrationEventTables.InboxTable(dbContext)} (event_id, handler, handled_at)
                  VALUES (@eventId, @handler, now())
                  """,
            parameters: InboxKeyOf(
                registration: registration,
                row: row
            ),
            cancellationToken: cancellationToken
        );

        transaction.Complete();
    }

    private ModuleDbContext ConsumerDbContextFor(
        IntegrationEventHandlerRegistration registration,
        IServiceProvider scope
    ) => ModuleDbContexts.Resolve(
        modules: modules,
        services: scope,
        moduleName: registration.ModuleName,
        purpose: "l'inbox du consommateur vit dans le schéma de son module, désigné par la registration "
        + $"de {registration.HandlerType.Name}"
    );

    private static Task<bool> IsAlreadyHandledAsync(
        ModuleDbContext dbContext,
        IntegrationEventHandlerRegistration registration,
        OutboxRow row,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  SELECT count(*) FROM {IntegrationEventTables.InboxTable(dbContext)}
                  WHERE event_id = @eventId AND handler = @handler
                  """,
            parameters: InboxKeyOf(
                registration: registration,
                row: row
            ),
            execute: async (
                command,
                token
            ) => (long)(await command.ExecuteScalarAsync(token))! > 0,
            cancellationToken: cancellationToken
        );

    // Même chemin que DomainEventDispatcher : l'interface fermée est invoquée
    // par réflexion, et la TargetInvocationException est déballée pour rendre
    // l'exception d'origine du handler, pile incluse.
    private static async Task InvokeHandlerAsync(
        IntegrationEventHandlerRegistration registration,
        IServiceProvider scope,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        var handlerInterface = typeof(IIntegrationEventHandler<>).MakeGenericType(registration.EventType);
        var handleMethod = handlerInterface.GetMethod(
            nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync)
        )!;

        try {
            await (Task)handleMethod.Invoke(
                obj: scope.GetRequiredService(registration.HandlerType),
                parameters: [integrationEvent, cancellationToken]
            )!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null) {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    // La réservation du lot : les lignes éligibles — en attente, ré-éligibles
    // au backoff, sans bail en cours — reçoivent un bail en une seule
    // instruction, donc une seule transaction, courte : SKIP LOCKED écarte
    // celles qu'une passe simultanée est en train de réserver, et le bail
    // posé les écarte des passes suivantes. RETURNING ne garantit pas
    // l'ordre, il est rétabli en mémoire.
    private async Task<IReadOnlyList<OutboxRow>> ReserveBatchAsync(
        IHostModule module,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContext = ModuleDbContexts.Resolve(
            services: scope.ServiceProvider,
            module: module
        );

        return await ModuleSql.ExecuteAsync(
            dbContext: dbContext,
            sql: $"""
                  UPDATE {IntegrationEventTables.OutboxTable(dbContext)}
                  SET reserved_until = now() + make_interval(secs => @reservationSeconds)
                  WHERE id IN (
                      SELECT id FROM {IntegrationEventTables.OutboxTable(dbContext)}
                      WHERE dispatched_at IS NULL AND poisoned_at IS NULL AND next_attempt_at <= now()
                        AND (reserved_until IS NULL OR reserved_until < now())
                      ORDER BY occurred_at, id
                      LIMIT 100
                      FOR UPDATE SKIP LOCKED
                  )
                  RETURNING id, discriminant, payload, attempts, occurred_at
                  """,
            parameters: new Dictionary<string, object> {
                ["reservationSeconds"] = options.Value.ReservationSeconds,
            },
            execute: async (
                command,
                token
            ) => {
                var rows = new List<(DateTime OccurredAt, OutboxRow Row)>();

                await using var reader = await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token)) {
                    rows.Add((
                        reader.GetDateTime(4),
                        new OutboxRow(
                            Id: reader.GetGuid(0),
                            Discriminant: reader.GetString(1),
                            Payload: reader.GetString(2),
                            Attempts: reader.GetInt32(3)
                        )
                    ));
                }

                return (IReadOnlyList<OutboxRow>)rows
                    .OrderBy(entry => entry.OccurredAt)
                    .ThenBy(entry => entry.Row.Id)
                    .Select(entry => entry.Row)
                    .ToList();
            },
            cancellationToken: cancellationToken
        );
    }

    private async Task MarkDispatchedAsync(
        IHostModule publisherModule,
        OutboxRow row,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContext = ModuleDbContexts.Resolve(
            services: scope.ServiceProvider,
            module: publisherModule
        );

        await ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  UPDATE {IntegrationEventTables.OutboxTable(dbContext)}
                  SET dispatched_at = now(), reserved_until = NULL
                  WHERE id = @id
                  """,
            parameters: new Dictionary<string, object> {
                ["id"] = row.Id,
            },
            cancellationToken: cancellationToken
        );
    }

    private async Task RecordFailureAsync(
        IHostModule publisherModule,
        OutboxRow row,
        Exception failure,
        CancellationToken cancellationToken
    )
    {
        var attempts = row.Attempts + 1;
        var poisoned = attempts >= options.Value.MaxAttempts;

        if (poisoned) {
            logger.LogError(
                exception: failure,
                message: "L'event {Discriminant} {EventId} est marqué poison après {Attempts} tentatives.",
                row.Discriminant,
                row.Id,
                attempts
            );
        }

        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContext = ModuleDbContexts.Resolve(
            services: scope.ServiceProvider,
            module: publisherModule
        );

        await ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  UPDATE {IntegrationEventTables.OutboxTable(dbContext)}
                  SET attempts = @attempts,
                      last_error = @lastError,
                      next_attempt_at = now() + make_interval(secs => @delaySeconds),
                      poisoned_at = CASE WHEN @poisoned THEN now() END,
                      reserved_until = NULL
                  WHERE id = @id
                  """,
            parameters: new Dictionary<string, object> {
                ["id"] = row.Id,
                ["attempts"] = attempts,
                ["lastError"] = failure.ToString(),
                ["delaySeconds"] = options.Value.BackoffDelaySecondsFor(attempts),
                ["poisoned"] = poisoned,
            },
            cancellationToken: cancellationToken
        );
    }

    private static Dictionary<string, object> InboxKeyOf(
        IntegrationEventHandlerRegistration registration,
        OutboxRow row
    ) =>
        new() {
            ["eventId"] = row.Id,
            ["handler"] = registration.HandlerType.FullName!,
        };

    private sealed record OutboxRow(
        Guid Id,
        string Discriminant,
        string Payload,
        int Attempts
    );
}
