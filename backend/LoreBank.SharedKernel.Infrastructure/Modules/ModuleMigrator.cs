using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LoreBank.SharedKernel.Infrastructure.Modules;

// Le mécanisme de migration, jumeau du seam : pour chaque module, fusionner
// les migrations de schéma EF pending et les migrations de données pending en
// une seule timeline ordonnée, et l'appliquer pas à pas (ADR 0013) — le
// schéma via IMigrator ciblé, les données via DataMigrationRunner. La
// politique — quand migrer — appartient aux consommateurs : le verbe
// `migrate` de l'hôte, et le harnais d'intégration qui prépare son
// Testcontainer. Le démarrage de l'API, lui, ne migre jamais (ADR 0006).
public static class ModuleMigrator
{
    public static async Task MigrateAsync(
        IServiceProvider services,
        IEnumerable<IHostModule> modules
    )
    {
        foreach (var module in modules) {
            await using var runner = DataMigrationRunner.Create(
                services: services,
                dbContextType: module.DbContextType
            );

            var pendingSchemaIds = await runner.DbContext.Database.GetPendingMigrationsAsync();
            var dataMigrationsById = DataMigrations
                .DiscoverIn(module.DbContextType.Assembly)
                .ToDictionary(keySelector: DataMigrations.IdOf);
            var appliedDataIds = await runner.GetAppliedIdsAsync(CancellationToken.None);

            var timeline = MigrationTimeline.Merge(
                schemaMigrationIds: pendingSchemaIds,
                dataMigrationIds: dataMigrationsById.Keys.Except(appliedDataIds)
            );

            foreach (var step in timeline) {
                if (step.IsData) {
                    await runner.ApplyAsync(
                        migrationType: dataMigrationsById[step.Id],
                        cancellationToken: CancellationToken.None
                    );
                } else {
                    await runner.DbContext
                        .GetService<IMigrator>()
                        .MigrateAsync(targetMigration: step.Id);
                }
            }

            // L'outbox et l'inbox du module, tables du socle créées ici comme
            // le journal des data migrations : aucune migration EF à générer
            // par module, et le démarrage de l'API ne crée toujours rien.
            // Chaque store porte le DDL de sa table ; le schéma, qui n'est ni
            // à l'une ni à l'autre, est posé ici — un module sans migration EF
            // n'en a pas encore.
            await ModuleSql.ExecuteNonQueryAsync(
                dbContext: runner.DbContext,
                sql: $"CREATE SCHEMA IF NOT EXISTS {runner.DbContext.Schema};",
                parameters: new Dictionary<string, object>(),
                cancellationToken: CancellationToken.None
            );
            await Outbox.EnsureTableAsync(
                dbContext: runner.DbContext,
                cancellationToken: CancellationToken.None
            );
            await Inbox.EnsureTableAsync(
                dbContext: runner.DbContext,
                cancellationToken: CancellationToken.None
            );
        }
    }
}
