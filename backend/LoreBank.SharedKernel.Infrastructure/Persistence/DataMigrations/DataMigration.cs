using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    // Mêmes règles que ModuleReader : la connexion est empruntée au DbContext,
    // jamais ouverte en propre, et Open/CloseConnectionAsync sont comptés par
    // EF. La commande est en plus enrôlée dans la transaction ouverte par
    // DataMigrationRunner — sans quoi elle écrirait hors du tout-ou-rien.
    protected async Task<List<TRow>> QueryAsync<TRow>(
        string sql,
        Dictionary<string, object> parameters,
        Func<DbDataReader, TRow> map,
        CancellationToken cancellationToken
    )
    {
        await context.Database.OpenConnectionAsync(cancellationToken);

        try {
            await using var command = CreateCommand(
                sql: sql,
                parameters: parameters
            );

            var rows = new List<TRow>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken)) {
                rows.Add(map(reader));
            }

            return rows;
        }
        finally {
            await context.Database.CloseConnectionAsync();
        }
    }

    protected async Task<int> ExecuteSqlAsync(
        string sql,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken
    )
    {
        await context.Database.OpenConnectionAsync(cancellationToken);

        try {
            await using var command = CreateCommand(
                sql: sql,
                parameters: parameters
            );

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally {
            await context.Database.CloseConnectionAsync();
        }
    }

    private DbCommand CreateCommand(
        string sql,
        Dictionary<string, object> parameters
    )
    {
        var command = context.Database.GetDbConnection().CreateCommand();

        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        // Le SQL nomme ses paramètres @xxx ; ici la clé est nue («id», pas
        // «@id») — l'asymétrie vit dans cette boucle, pas dans les migrations.
        foreach (var (name, value) in parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
