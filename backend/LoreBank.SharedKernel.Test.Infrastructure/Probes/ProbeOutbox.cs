using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Les gestes étendus des tests d'outbox du socle — rejeu, inbox, poison —
// bâtis sur le cœur d'emprunt de connexion d'OutboxProbe. Étendus et
// internes à dessein : un module publieur n'a que la surface publique
// d'OutboxProbe, ces gestes-ci re-prouveraient des invariants du socle. Les
// sondes écrivent pour de vrai dans probe.__outbox / probe.__inbox, d'où le
// nettoyage ciblé sur les discriminants de sonde.
internal static class ProbeOutbox
{
    internal sealed record Row(
        Guid Id,
        int Attempts,
        bool Dispatched,
        bool Poisoned,
        string? LastError
    );

    internal static async Task CleanAsync(IntegrationTestWebAppFactory factory)
    {
        await ExecuteAsync(
            factory: factory,
            action: async (
                dbContext,
                command
            ) => {
                command.CommandText =
                    $"""
                     DELETE FROM {dbContext.Schema}.__inbox WHERE handler LIKE '%Probe%';
                     DELETE FROM {dbContext.Schema}.__outbox WHERE discriminant LIKE '%probe-%';
                     """;

                await command.ExecuteNonQueryAsync();

                return 0;
            }
        );
    }

    internal static Task<Row?> FindRowAsync(
        IntegrationTestWebAppFactory factory,
        string discriminant
    ) =>
        ExecuteAsync(
            factory: factory,
            action: async (
                dbContext,
                command
            ) => {
                command.CommandText =
                    $"""
                     SELECT id, attempts, dispatched_at IS NOT NULL, poisoned_at IS NOT NULL, last_error
                     FROM {dbContext.Schema}.__outbox WHERE discriminant = @discriminant
                     """;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "discriminant";
                parameter.Value = discriminant;
                command.Parameters.Add(parameter);

                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync()) {
                    return (Row?)null;
                }

                return new Row(
                    Id: reader.GetGuid(0),
                    Attempts: reader.GetInt32(1),
                    Dispatched: reader.GetBoolean(2),
                    Poisoned: reader.GetBoolean(3),
                    LastError: reader.IsDBNull(4) ? null : reader.GetString(4)
                );
            }
        );

    internal static Task<long> CountInboxAsync(
        IntegrationTestWebAppFactory factory,
        Guid eventId
    ) =>
        ExecuteAsync(
            factory: factory,
            action: async (
                dbContext,
                command
            ) => {
                command.CommandText =
                    $"SELECT count(*) FROM {dbContext.Schema}.__inbox WHERE event_id = @eventId";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "eventId";
                parameter.Value = eventId;
                command.Parameters.Add(parameter);

                return (long)(await command.ExecuteScalarAsync())!;
            }
        );

    // Simule la redélivrance : la ligne redevient pending, comme après un
    // crash tombé entre le commit des handlers et le marquage de l'outbox.
    internal static Task RedeliverAsync(
        IntegrationTestWebAppFactory factory,
        Guid id
    ) =>
        ExecuteAsync(
            factory: factory,
            action: async (
                dbContext,
                command
            ) => {
                command.CommandText =
                    $"UPDATE {dbContext.Schema}.__outbox SET dispatched_at = NULL WHERE id = @id";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "id";
                parameter.Value = id;
                command.Parameters.Add(parameter);

                return await command.ExecuteNonQueryAsync();
            }
        );

    private static Task<T> ExecuteAsync<T>(
        IntegrationTestWebAppFactory factory,
        Func<ProbeDbContext, System.Data.Common.DbCommand, Task<T>> action
    ) => OutboxProbe.ExecuteAsync(
        factory: factory,
        action: action
    );
}
