using System.Diagnostics;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// L'implémentation du port : une ligne dans l'outbox du module publieur,
// désigné par le premier segment du discriminant. Le DbContext est résolu
// dans le scope courant — celui de la commande — et la connexion empruntée
// s'enrôle dans son TransactionScope ambiant : l'event part avec la
// transaction de la commande ou pas du tout, c'est toute la promesse de
// l'outbox. La ligne garde aussi le traceparent W3C de l'Activity courante —
// celle que le hosting pose sur la requête — pour que le traitement du
// consommateur soit un enfant de la même trace (ADR 0025) ; nul hors
// activité (migration de données, test), sans erreur.
public sealed class OutboxPublisher(
    IEnumerable<IHostModule> modules,
    IServiceProvider serviceProvider
) : IIntegrationEventPublisher
{
    public async Task PublishAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        var discriminant = IntegrationEventDiscriminant.Of(integrationEvent.GetType());
        var dbContext = PublisherDbContextFor(discriminant);

        await ModuleSql.ExecuteNonQueryAsync(
            dbContext: dbContext,
            sql: $"""
                  INSERT INTO {IntegrationEventTables.OutboxTable(dbContext)}
                      (id, discriminant, payload, occurred_at, next_attempt_at, attempts, trace_parent)
                  VALUES (@id, @discriminant, CAST(@payload AS jsonb), now(), now(), 0, @traceParent)
                  """,
            parameters: new Dictionary<string, object> {
                ["id"] = Guid.NewGuid(),
                ["discriminant"] = discriminant,
                ["payload"] = IntegrationEventJson.Serialize(integrationEvent),
                ["traceParent"] = (object?)Activity.Current?.Id ?? DBNull.Value,
            },
            cancellationToken: cancellationToken
        );
    }

    private ModuleDbContext PublisherDbContextFor(string discriminant) =>
        ModuleDbContexts.Resolve(
            modules: modules,
            services: serviceProvider,
            moduleName: IntegrationEventDiscriminant.ModuleOf(discriminant),
            purpose: "un integration event se publie depuis l'outbox de son module, désigné par le premier "
            + $"segment du discriminant « {discriminant} »"
        );
}
