using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La surface de rejeu des migrations de données offerte aux modules : le test
// d'une migration (ADR 0013) se réduit à arranger des lignes en SQL brut,
// rejouer, relire — sans une ligne de plomberie ADO. Le geste d'emprunt est
// celui du socle (ModuleSql), invisible des assemblies de test de module :
// c'est ce qui rendait la sonde nécessaire, et ce qui faisait recopier six
// fois le même `OpenConnection / CreateCommand / finally`.
//
// Générique par classe et non par méthode : le SQL d'un arrange est écrit
// dans une lambda, dont le type du paramètre ne peut pas s'inférer. Ancrer le
// DbContext sur la classe laisse les appels lisibles —
// `DataMigrationProbe<BankDbContext>.ReadAsync<DateTimeOffset?>(…)`.
//
// Le SQL reçoit le schéma du module, jamais le DbContext : un test de
// migration n'a rien d'autre à en tirer, et le schéma est dérivé de
// l'identité (ADR 0009), il ne s'écrit pas en dur.
public static class DataMigrationProbe<TDbContext>
    where TDbContext : ModuleDbContext
{
    // Arranger, nettoyer : les lignes d'un test de migration ne peuvent pas
    // passer par les use cases, c'est leur raison d'être — le modèle vivant
    // ne sait plus matérialiser la forme qu'elles portent.
    public static Task ExecuteAsync(
        IntegrationTestWebAppFactory factory,
        Func<string, string> sqlFor,
        IReadOnlyDictionary<string, object>? parameters = null
    ) =>
        OnDbContextAsync(
            factory: factory,
            action: dbContext => ModuleSql.ExecuteNonQueryAsync(
                dbContext: dbContext,
                sql: sqlFor(dbContext.Schema),
                parameters: parameters ?? new Dictionary<string, object>(),
                cancellationToken: CancellationToken.None
            )
        );

    // Relire une colonne. Npgsql rend un DateTime pour un timestamptz : quand
    // le test demande l'Instant (ADR 0024), la sonde le rend sous la forme du
    // domaine plutôt que de laisser chaque test refaire la conversion.
    public static Task<T?> ReadAsync<T>(
        IntegrationTestWebAppFactory factory,
        Func<string, string> sqlFor,
        IReadOnlyDictionary<string, object>? parameters = null
    ) =>
        OnDbContextAsync(
            factory: factory,
            action: dbContext => ModuleSql.ExecuteAsync(
                dbContext: dbContext,
                sql: sqlFor(dbContext.Schema),
                parameters: parameters ?? new Dictionary<string, object>(),
                execute: async (
                    command,
                    token
                ) => Convert<T>(await command.ExecuteScalarAsync(token)),
                cancellationToken: CancellationToken.None
            )
        );

    // Rejouer la migration. La sonde ne devine pas ses dépendances : les
    // migrations n'ont pas les mêmes, et l'horloge figée reste une décision du
    // test — c'est elle qu'il affirme ensuite.
    public static Task ReplayAsync(
        IntegrationTestWebAppFactory factory,
        Func<TDbContext, DataMigration> migrationFor
    ) =>
        OnDbContextAsync(
            factory: factory,
            action: async dbContext =>
            {
                await migrationFor(dbContext).ExecuteAsync(CancellationToken.None);

                return 0;
            }
        );

    // Le maillon central du triptyque (ADR 0013) : un backfill lit une forme
    // intermédiaire — la colonne encore nullable — que la timeline complète du
    // conteneur ne connaît plus. La contrainte est relâchée le temps de
    // l'action et rétablie dans un finally : le seul endroit du repo qui casse
    // volontairement le schéma ne dépend pas d'un TearDown qu'il faut penser à
    // écrire. Le rétablissement suppose qu'aucun NULL ne reste — c'est
    // exactement ce qu'un backfill garantit, et ce que son test affirme.
    public static async Task WithNullableColumnAsync(
        IntegrationTestWebAppFactory factory,
        string table,
        string column,
        Func<Task> action
    )
    {
        await AlterNullabilityAsync(
            factory: factory,
            table: table,
            column: column,
            clause: "DROP NOT NULL"
        );

        try {
            await action();
        } finally {
            await AlterNullabilityAsync(
                factory: factory,
                table: table,
                column: column,
                clause: "SET NOT NULL"
            );
        }
    }

    private static Task AlterNullabilityAsync(
        IntegrationTestWebAppFactory factory,
        string table,
        string column,
        string clause
    ) =>
        ExecuteAsync(
            factory: factory,
            sqlFor: schema => $"ALTER TABLE {schema}.{table} ALTER COLUMN {column} {clause}"
        );

    private static T? Convert<T>(object? value)
    {
        if (value is null or DBNull) {
            return default;
        }

        // Le cas nommé : l'Instant, rendu par Npgsql en DateTime. Le test
        // demande le plus souvent un DateTimeOffset? — c'est le type sous le
        // Nullable qu'il faut regarder.
        var asked = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (value is DateTime instant && asked == typeof(DateTimeOffset)) {
            return (T) (object) new DateTimeOffset(instant);
        }

        return (T) value;
    }

    private static async Task<T> OnDbContextAsync<T>(
        IntegrationTestWebAppFactory factory,
        Func<TDbContext, Task<T>> action
    )
    {
        using var scope = factory.Services.CreateScope();

        return await action(scope.ServiceProvider.GetRequiredService<TDbContext>());
    }
}
