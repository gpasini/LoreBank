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
// l'outbox.
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
                      (id, discriminant, payload, occurred_at, next_attempt_at, attempts)
                  VALUES (@id, @discriminant, CAST(@payload AS jsonb), now(), now(), 0)
                  """,
            parameters: new Dictionary<string, object> {
                ["id"] = Guid.NewGuid(),
                ["discriminant"] = discriminant,
                ["payload"] = IntegrationEventJson.Serialize(integrationEvent),
            },
            cancellationToken: cancellationToken
        );
    }

    private ModuleDbContext PublisherDbContextFor(string discriminant)
    {
        var moduleName = IntegrationEventDiscriminant.ModuleOf(discriminant);
        var module = modules.SingleOrDefault(candidate => candidate.ModuleName.Equals(
            value: moduleName,
            comparisonType: StringComparison.OrdinalIgnoreCase
        ));

        if (module is null) {
            throw new InvalidOperationException(
                $"Le discriminant « {discriminant} » désigne le module « {moduleName} », qui n'est monté par "
                + "aucun IHostModule : un integration event se publie depuis l'outbox de son module — le "
                + "premier segment du discriminant doit nommer un module de HostModules.All."
            );
        }

        return (ModuleDbContext)serviceProvider.GetRequiredService(module.DbContextType);
    }
}
