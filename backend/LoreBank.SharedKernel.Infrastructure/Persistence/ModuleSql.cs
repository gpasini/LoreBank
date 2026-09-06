using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LoreBank.SharedKernel.Infrastructure.Persistence;

// LE geste SQL du socle — readers, migrations de données, outbox et inbox
// passent tous ici : la connexion est empruntée au DbContext du module,
// jamais ouverte en propre (une seconde connexion sous le TransactionScope
// ambiant d'une commande ferait enrôler un second connecteur, et la
// transaction escaladerait en distribué — non supporté hors Windows), et
// refermée dans un finally — Open/CloseConnectionAsync sont comptés par EF,
// ils n'ouvrent ni ne ferment rien si EF tient déjà la connexion.
// ModuleSqlTest épingle ces invariants une fois pour les quatre canaux.
internal static class ModuleSql
{
    internal static async Task<T> ExecuteAsync<T>(
        ModuleDbContext dbContext,
        string sql,
        IReadOnlyDictionary<string, object> parameters,
        Func<DbCommand, CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken
    )
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = sql;

            // L'enrôlement, fait unique : indispensable sous le
            // BeginTransaction du DataMigrationRunner — sans lui la commande
            // écrirait hors du tout-ou-rien — et no-op partout ailleurs
            // (CurrentTransaction est nul sous le seul TransactionScope
            // ambiant, où la connexion s'enrôle d'elle-même). Corollaire :
            // aucun appelant ne peut écrire à côté d'une transaction EF
            // explicite sans rien pour le signaler.
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

            // Le SQL nomme ses paramètres @xxx ; ici la clé est nue («id»,
            // pas «@id») — l'asymétrie vit dans cette boucle, une fois.
            foreach (var (name, value) in parameters) {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            return await execute(
                arg1: command,
                arg2: cancellationToken
            );
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    internal static Task<int> ExecuteNonQueryAsync(
        ModuleDbContext dbContext,
        string sql,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken
    ) =>
        ExecuteAsync(
            dbContext: dbContext,
            sql: sql,
            parameters: parameters,
            execute: (
                command,
                token
            ) => command.ExecuteNonQueryAsync(token),
            cancellationToken: cancellationToken
        );
}
