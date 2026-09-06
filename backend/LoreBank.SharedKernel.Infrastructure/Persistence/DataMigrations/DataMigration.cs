using System.Data.Common;

namespace LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

// La base des migrations de données d'un module (ADR 0013) : un maillon de la
// même timeline que les migrations de schéma, écrit en C# avec le code vivant
// d'aujourd'hui — la logique métier (value objects, calculs) ne se recopie
// jamais en SQL. Le SQL de bordure reste permis, via les helpers ci-dessous,
// pour lire ou écrire une forme intermédiaire que le modèle vivant ne sait
// plus matérialiser. Corollaire du code vivant : une migration appliquée sur
// tous les environnements est un artefact mort, supprimable avec sa ligne de
// journal.
public abstract class DataMigration(ModuleDbContext context)
{
    // Le schéma du module, à interpoler dans le SQL (FROM {Schema}.xxx) : un
    // fait dérivé de l'identité (ADR 0009) ne se réécrit pas en dur.
    protected string Schema => context.Schema;

    public abstract Task ExecuteAsync(CancellationToken cancellationToken);

    // Le geste SQL vit dans ModuleSql : connexion empruntée, finally compté
    // par EF, clés de paramètres nues — et l'enrôlement dans la transaction
    // ouverte par DataMigrationRunner, sans quoi la commande écrirait hors du
    // tout-ou-rien.
    protected Task<List<TRow>> QueryAsync<TRow>(
        string sql,
        Dictionary<string, object> parameters,
        Func<DbDataReader, TRow> map,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteAsync(
            dbContext: context,
            sql: sql,
            parameters: parameters,
            execute: async (
                command,
                token
            ) =>
            {
                var rows = new List<TRow>();

                await using var reader = await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token)) {
                    rows.Add(map(reader));
                }

                return rows;
            },
            cancellationToken: cancellationToken
        );

    protected Task<int> ExecuteSqlAsync(
        string sql,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken
    ) =>
        ModuleSql.ExecuteNonQueryAsync(
            dbContext: context,
            sql: sql,
            parameters: parameters,
            cancellationToken: cancellationToken
        );
}
