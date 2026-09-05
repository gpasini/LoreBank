using System.Data.Common;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Infrastructure.Readers;

// La base des readers d'un module : elle porte l'emprunt de connexion, pour
// qu'un reader concret ne fournisse que son SQL, ses paramètres et sa lecture
// de colonnes. Rien ici n'est spécifique à un provider — c'est ce qui permet
// de la tester en unitaire sur Sqlite (ModuleReaderTest).
public abstract class ModuleReader(ModuleDbContext context)
{
    // Le schéma du module, à interpoler dans le SQL (FROM {Schema}.xxx) : un
    // fait dérivé de l'identité (ADR 0009) ne se réécrit pas en dur.
    protected string Schema => context.Schema;

    // La connexion est empruntée au DbContext, jamais ouverte en propre : une
    // seconde connexion vers le même PostgreSQL sous le TransactionScope
    // ambiant d'une commande ferait enrôler un second connecteur, et la
    // transaction escaladerait en distribué — non supporté hors Windows.
    //
    // Open/CloseConnectionAsync sont comptés par EF : ils n'ouvrent ni ne
    // ferment rien si EF tient déjà la connexion.
    protected async Task<TRow?> QuerySingleOrDefaultAsync<TRow>(
        string sql,
        Dictionary<string, object> parameters,
        Func<DbDataReader, TRow> map,
        CancellationToken cancellationToken
    ) where TRow : class
    {
        await context.Database.OpenConnectionAsync(cancellationToken);

        try {
            await using var command = CreateCommand(
                sql: sql,
                parameters: parameters
            );

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            return await reader.ReadAsync(cancellationToken)
                ? map(reader)
                : null;
        }
        finally {
            await context.Database.CloseConnectionAsync();
        }
    }

    // La variante liste : mêmes invariants d'emprunt, une ligne du Result par
    // ligne SQL — une liste vide est un résultat normal, jamais null.
    protected async Task<IReadOnlyList<TRow>> QueryAsync<TRow>(
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

    private DbCommand CreateCommand(
        string sql,
        Dictionary<string, object> parameters
    )
    {
        var command = context.Database.GetDbConnection().CreateCommand();

        command.CommandText = sql;

        // Le SQL nomme ses paramètres @xxx ; ici la clé est nue («id»,
        // pas «@id») — l'asymétrie vit dans cette boucle, pas dans les
        // readers.
        foreach (var (name, value) in parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
