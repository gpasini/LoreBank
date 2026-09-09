using System.Data.Common;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La surface d'observation d'outbox offerte aux modules publieurs : leur
// garde-fou « mes jumeaux partent vraiment dans mon outbox » (ADR 0014) se
// réduit à agir en HTTP, lire les lignes, affirmer discriminant, payload et
// ressource de Signal (ADR 0026) —
// plus de plomberie ADO à recopier par module (voir
// IntegrationEventPublicationTest, le consommateur de référence). Attempts,
// poison, rejeu et inbox n'y sont volontairement pas : ce sont des
// invariants du socle, prouvés par OutboxProcessorTest sur le terrain probe
// — pas ceux d'un module.
public static class OutboxProbe
{
    public sealed record Row(
        string Discriminant,
        string Payload,
        bool Dispatched,
        string? ResourceKind,
        Guid? ResourceId
    );

    public static Task<IReadOnlyList<Row>> ReadRowsAsync<TDbContext>(IntegrationTestWebAppFactory factory)
        where TDbContext : ModuleDbContext
        =>
        ExecuteAsync(
            factory: factory,
            action: async (
                TDbContext dbContext,
                DbCommand command
            ) =>
            {
                command.CommandText =
                    $"SELECT discriminant, payload, dispatched_at IS NOT NULL, resource_kind, resource_id FROM {dbContext.Schema}.__outbox";

                var rows = new List<Row>();

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync()) {
                    rows.Add(new Row(
                        Discriminant: reader.GetString(0),
                        Payload: reader.GetString(1),
                        Dispatched: reader.GetBoolean(2),
                        ResourceKind: reader.IsDBNull(3) ? null : reader.GetString(3),
                        ResourceId: reader.IsDBNull(4) ? null : reader.GetGuid(4)
                    ));
                }

                return (IReadOnlyList<Row>)rows;
            }
        );

    // Vide l'outbox entière du module : les fixtures HTTP écrivent pour de
    // vrai dans le conteneur partagé, un test ne compte que ses propres
    // lignes en nettoyant avant et après. L'exécution en série
    // (Parallelizable.None, vérifié par BaseHostTest) rend le vidage sans
    // danger.
    public static Task CleanAsync<TDbContext>(IntegrationTestWebAppFactory factory)
        where TDbContext : ModuleDbContext
        =>
        ExecuteAsync(
            factory: factory,
            action: async (
                TDbContext dbContext,
                DbCommand command
            ) =>
            {
                command.CommandText = $"DELETE FROM {dbContext.Schema}.__outbox";

                return await command.ExecuteNonQueryAsync();
            }
        );

    // Le cœur de toutes les observations d'outbox du harnais, ProbeOutbox
    // compris : scope, DbContext du module résolu, connexion empruntée —
    // jamais ouverte en propre, une seconde connexion sous un
    // TransactionScope ambiant escaladerait en distribué — refermée dans un
    // finally (EF compte les ouvertures).
    internal static async Task<T> ExecuteAsync<TDbContext, T>(
        IntegrationTestWebAppFactory factory,
        Func<TDbContext, DbCommand, Task<T>> action
    )
        where TDbContext : ModuleDbContext
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            return await action(
                arg1: dbContext,
                arg2: command
            );
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
