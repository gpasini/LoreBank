using System.Data.Common;
using LoreBank.SharedKernel.Infrastructure.Persistence;

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

    // L'emprunt de connexion, le finally compté par EF, les clés de
    // paramètres nues et l'enrôlement de transaction vivent dans ModuleSql —
    // le geste SQL unique du socle. Ne reste ici que la forme d'une lecture :
    // une ligne ou null.
    protected Task<TRow?> QuerySingleOrDefaultAsync<TRow>(
        string sql,
        Dictionary<string, object> parameters,
        Func<DbDataReader, TRow> map,
        CancellationToken cancellationToken
    ) where TRow : class =>
        ModuleSql.ExecuteAsync(
            dbContext: context,
            sql: sql,
            parameters: parameters,
            execute: async (
                command,
                token
            ) =>
            {
                await using var reader = await command.ExecuteReaderAsync(token);

                return await reader.ReadAsync(token)
                    ? map(reader)
                    : null;
            },
            cancellationToken: cancellationToken
        );

    // La variante liste : mêmes invariants d'emprunt, une ligne du Result par
    // ligne SQL — une liste vide est un résultat normal, jamais null.
    protected Task<IReadOnlyList<TRow>> QueryAsync<TRow>(
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

                return (IReadOnlyList<TRow>)rows;
            },
            cancellationToken: cancellationToken
        );
}
