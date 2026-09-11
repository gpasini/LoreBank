using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La surface d'observation d'outbox offerte aux modules publieurs : leur
// garde-fou « mes jumeaux partent vraiment dans mon outbox » (ADR 0014) se
// réduit à agir en HTTP, lire les lignes, affirmer discriminant, payload et
// ressource de Signal (ADR 0026) — plus de plomberie ADO à recopier par
// module (voir IntegrationEventPublicationTest, le consommateur de
// référence). Attempts, poison, rejeu et inbox n'y sont volontairement pas :
// ce sont des invariants du socle, prouvés par OutboxProcessorTest sur le
// terrain probe — pas ceux d'un module.
//
// La sonde ne sait plus comment une ligne est faite : elle lit par le store
// du socle et ne garde que la projection qu'un module a le droit de voir.
public static class OutboxProbe
{
    public sealed record Row(
        string Discriminant,
        string Payload,
        bool Dispatched,
        string? ResourceKind,
        Guid? ResourceId
    );

    // Une passe de livraison, pilotée à la main : la cadence de fond est
    // neutralisée par le harnais, et un module qui prouve qu'il emprunte le
    // chemin fait passer le dispatcher lui-même. Jumeau de
    // SignalProbe.TailAsync — un module n'a pas à connaître le nom du
    // service qui dépile. Non générique : la passe traverse toutes les
    // outbox montées, pas celle d'un module.
    public static Task DeliverAsync(IntegrationTestWebAppFactory factory) =>
        factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

    public static async Task<IReadOnlyList<Row>> ReadRowsAsync<TDbContext>(IntegrationTestWebAppFactory factory)
        where TDbContext : ModuleDbContext
    {
        var rows = await OnOutboxAsync<TDbContext, IReadOnlyList<Outbox.Row>>(
            factory: factory,
            action: outbox => outbox.ReadAllAsync(CancellationToken.None)
        );

        return rows
            .Select(row => new Row(
                    Discriminant: row.Discriminant,
                    Payload: row.Payload,
                    Dispatched: row.Dispatched,
                    ResourceKind: row.ResourceKind,
                    ResourceId: row.ResourceId
                )
            )
            .ToList();
    }

    // Vide l'outbox entière du module : les fixtures HTTP écrivent pour de
    // vrai dans le conteneur partagé, un test ne compte que ses propres
    // lignes en nettoyant avant et après. L'exécution en série
    // (Parallelizable.None, vérifié par BaseHostTest) rend le vidage sans
    // danger. Vider n'est pas un geste de production : il passe par le geste
    // SQL du socle et le nom de table du store, pas par un verbe.
    public static Task CleanAsync<TDbContext>(IntegrationTestWebAppFactory factory)
        where TDbContext : ModuleDbContext
        =>
        OnDbContextAsync<TDbContext, int>(
            factory: factory,
            action: dbContext => ModuleSql.ExecuteNonQueryAsync(
                dbContext: dbContext,
                sql: $"DELETE FROM {Outbox.TableOf(dbContext)}",
                parameters: new Dictionary<string, object>(),
                cancellationToken: CancellationToken.None
            )
        );

    // Le store d'un module, dans un scope à soi : le cœur de toutes les
    // observations d'outbox du harnais, ProbeOutbox compris. La connexion est
    // empruntée par le store lui-même — jamais ouverte ici.
    internal static Task<T> OnOutboxAsync<TDbContext, T>(
        IntegrationTestWebAppFactory factory,
        Func<Outbox, Task<T>> action
    )
        where TDbContext : ModuleDbContext
        =>
        OnDbContextAsync<TDbContext, T>(
            factory: factory,
            action: dbContext => action(new Outbox(dbContext))
        );

    internal static Task<T> OnInboxAsync<TDbContext, T>(
        IntegrationTestWebAppFactory factory,
        Func<Inbox, Task<T>> action
    )
        where TDbContext : ModuleDbContext
        =>
        OnDbContextAsync<TDbContext, T>(
            factory: factory,
            action: dbContext => action(new Inbox(dbContext))
        );

    internal static async Task<T> OnDbContextAsync<TDbContext, T>(
        IntegrationTestWebAppFactory factory,
        Func<TDbContext, Task<T>> action
    )
        where TDbContext : ModuleDbContext
    {
        using var scope = factory.Services.CreateScope();

        return await action(scope.ServiceProvider.GetRequiredService<TDbContext>());
    }
}
