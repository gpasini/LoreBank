using System.Diagnostics;
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
// les tests la pilotent déterministiquement. Ce fichier n'écrit plus une
// ligne de SQL : la forme des lignes appartient aux stores (Outbox, Inbox),
// ce qui reste ici est l'orchestration et la politique.
//
// Livraison at-least-once : chaque handler s'exécute dans son propre scope DI
// et son propre TransactionScope, ligne d'inbox incluse — un handler qui
// échoue n'annule ni les autres ni la commande d'origine, il remet la ligne
// d'outbox en attente avec backoff, jusqu'au marquage poison. Le marquage
// « dispatché » de l'outbox est hors de ces transactions (porte
// InOwnScopeAsync : schéma du publieur, connexion distincte — la joindre
// ferait escalader en distribué) : un crash entre les deux rejoue l'event, et
// c'est l'inbox qui rend le rejeu inoffensif. Plusieurs instances de l'hôte
// dépilent la même outbox par Réservation (ADR 0021), le bail venant des
// options. Chaque exécution de handler est tracée (OutboxTracing, ADR 0025) :
// scope, transaction et ligne d'inbox sous une même activité, enfant de la
// commande d'origine.
public sealed class OutboxProcessor(
    IServiceProvider serviceProvider,
    IEnumerable<IHostModule> modules,
    IEnumerable<IntegrationEventHandlerRegistration> registrations,
    IntegrationEventStores stores,
    IOptions<OutboxOptions> options,
    OutboxMetrics metrics,
    ILogger<OutboxProcessor> logger
)
{
    private const int BatchSize = 100;

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        foreach (var module in modules) {
            var rows = await stores.InOwnScopeAsync(
                module: module,
                action: (
                    outbox,
                    _
                ) => outbox.ReserveAsync(
                    lease: options.Value.ReservationDuration,
                    batchSize: BatchSize,
                    cancellationToken: cancellationToken
                )
            );

            foreach (var row in rows) {
                await ProcessRowAsync(
                    publisherModule: module,
                    row: row,
                    cancellationToken: cancellationToken
                );
            }

            await MeasureAsync(
                module: module,
                cancellationToken: cancellationToken
            );
        }
    }

    // La Rétention (ADR 0021) : les lignes livrées et les traces d'inbox plus
    // vieilles que la rétention disparaissent — jamais une ligne en attente,
    // jamais une ligne poison (elle reste pour un humain). Les deux tables
    // sont balayées dans le même scope, et chaque suppression est idempotente :
    // deux instances qui purgent en même temps ne se gênent pas.
    public async Task PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        foreach (var module in modules) {
            await stores.InOwnScopeAsync(
                module: module,
                action: async (
                    outbox,
                    inbox
                ) =>
                {
                    await outbox.PurgeAsync(
                        retentionDays: options.Value.RetentionDays,
                        cancellationToken: cancellationToken
                    );
                    await inbox.PurgeAsync(
                        retentionDays: options.Value.RetentionDays,
                        cancellationToken: cancellationToken
                    );
                }
            );

            // La synthèse à la cadence de purge (ADR 0022) : les lignes poison
            // restent pour un humain, ce log est ce qui le prévient — rien
            // quand il n'y en a pas.
            var depth = await MeasureAsync(
                module: module,
                cancellationToken: cancellationToken
            );

            if (depth.Poisoned > 0) {
                logger.LogWarning(
                    message: "Le module {Module} a {Poisoned} ligne(s) poison dans son outbox, à examiner.",
                    module.ModuleName,
                    depth.Poisoned
                );
            }
        }
    }

    // La mesure d'une outbox : le store compte, le processor pousse dans les
    // jauges du socle — un store ne connaît pas les métriques.
    private async Task<OutboxDepth> MeasureAsync(
        IHostModule module,
        CancellationToken cancellationToken
    )
    {
        var depth = await stores.InOwnScopeAsync(
            module: module,
            action: (
                outbox,
                _
            ) => outbox.MeasureAsync(cancellationToken)
        );

        metrics.Record(
            moduleName: module.ModuleName,
            depth: depth
        );

        return depth;
    }

    private async Task ProcessRowAsync(
        IHostModule publisherModule,
        Outbox.Reserved row,
        CancellationToken cancellationToken
    )
    {
        Exception? firstFailure = null;

        foreach (var registration in registrations.Where(candidate => candidate.Discriminant == row.Discriminant)) {
            using var activity = OutboxTracing.StartHandling(
                publisherModule: publisherModule.ModuleName,
                consumerModule: registration.ModuleName,
                handler: registration.HandlerType.Name,
                discriminant: row.Discriminant,
                messageId: row.Id,
                attempt: row.Attempts + 1,
                traceParent: row.TraceParent
            );

            try {
                await HandleAsync(
                    registration: registration,
                    row: row,
                    cancellationToken: cancellationToken
                );
            } catch (Exception exception) {
                activity?.SetStatus(
                    code: ActivityStatusCode.Error,
                    description: exception.Message
                );
                activity?.AddException(exception);

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
            await stores.InOwnScopeAsync(
                module: publisherModule,
                action: (
                    outbox,
                    _
                ) => outbox.MarkDispatchedAsync(
                    id: row.Id,
                    cancellationToken: cancellationToken
                )
            );
        } else {
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
    // les écritures du handler, donc tout commite ou rien. C'est pourquoi
    // l'inbox se construit ici, sur ce DbContext, et non par les deux portes
    // d'IntegrationEventStores.
    private async Task HandleAsync(
        IntegrationEventHandlerRegistration registration,
        Outbox.Reserved row,
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

        var inbox = new Inbox(ConsumerDbContextFor(
                registration: registration,
                scope: scope.ServiceProvider
            )
        );

        if (await inbox.IsHandledAsync(
                eventId: row.Id,
                handler: registration.HandlerType.FullName!,
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

        await inbox.MarkHandledAsync(
            eventId: row.Id,
            handler: registration.HandlerType.FullName!,
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
            await (Task) handleMethod.Invoke(
                obj: scope.GetRequiredService(registration.HandlerType),
                parameters: [integrationEvent, cancellationToken]
            )!;
        } catch (TargetInvocationException exception) when (exception.InnerException is not null) {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    // La politique de reprise : le backoff exponentiel et le seuil de poison
    // se décident ici, à partir des options — le store écrit les valeurs
    // qu'on lui donne.
    private async Task RecordFailureAsync(
        IHostModule publisherModule,
        Outbox.Reserved row,
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

        await stores.InOwnScopeAsync(
            module: publisherModule,
            action: (
                outbox,
                _
            ) => outbox.RecordFailureAsync(
                id: row.Id,
                attempts: attempts,
                lastError: failure.ToString(),
                delaySeconds: options.Value.BackoffDelaySecondsFor(attempts),
                poisoned: poisoned,
                cancellationToken: cancellationToken
            )
        );
    }
}
