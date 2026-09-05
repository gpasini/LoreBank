using System.Data.Common;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// Le même geste que ModuleReader et les helpers de DataMigration : connexion
// empruntée au DbContext du module — jamais ouverte en propre, une seconde
// connexion sous un TransactionScope ambiant ferait escalader en distribué —
// refermée dans un finally (EF compte les ouvertures). Ouverte dans le scope
// ambiant, elle s'y enrôle d'elle-même : c'est ce qui rend l'écriture
// d'outbox atomique avec la commande, et la ligne d'inbox atomique avec le
// handler consommateur.
internal static class OutboxSql
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
