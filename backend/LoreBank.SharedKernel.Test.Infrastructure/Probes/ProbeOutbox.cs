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
        string? LastError,
        bool Reserved
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
                     SELECT id, attempts, dispatched_at IS NOT NULL, poisoned_at IS NOT NULL, last_error,
                            reserved_until IS NOT NULL
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
                    LastError: reader.IsDBNull(4) ? null : reader.GetString(4),
                    Reserved: reader.GetBoolean(5)
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

    // Pose ou lève un bail à la main : une réservation dans le futur simule
    // une autre instance en plein traitement, dans le passé une instance
    // disparue dont le bail a expiré.
    internal static Task ReserveAsync(
        IntegrationTestWebAppFactory factory,
        Guid id,
        TimeSpan fromNow
    ) =>
        ExecuteAsync(
            factory: factory,
            action: async (
                dbContext,
                command
            ) => {
                command.CommandText =
                    $"""
                     UPDATE {dbContext.Schema}.__outbox
                     SET reserved_until = now() + make_interval(secs => @seconds)
                     WHERE id = @id
                     """;

                var id_ = command.CreateParameter();
                id_.ParameterName = "id";
                id_.Value = id;
                command.Parameters.Add(id_);

                var seconds = command.CreateParameter();
                seconds.ParameterName = "seconds";
                seconds.Value = fromNow.TotalSeconds;
                command.Parameters.Add(seconds);

                return await command.ExecuteNonQueryAsync();
            }
        );

    // Vieillit une ligne et ses traces d'inbox d'autant : la Rétention se
    // prouve sans attendre, en antidatant ce que le processor a écrit.
    internal static Task BackdateAsync(
        IntegrationTestWebAppFactory factory,
        Guid id,
        TimeSpan by
    ) =>
        ExecuteAsync(
            factory: factory,
            action: async (
                dbContext,
                command
            ) => {
                command.CommandText =
                    $"""
                     UPDATE {dbContext.Schema}.__outbox
                     SET occurred_at = occurred_at - make_interval(secs => @seconds),
                         next_attempt_at = next_attempt_at - make_interval(secs => @seconds),
                         dispatched_at = dispatched_at - make_interval(secs => @seconds),
                         poisoned_at = poisoned_at - make_interval(secs => @seconds)
                     WHERE id = @id;
                     UPDATE {dbContext.Schema}.__inbox
                     SET handled_at = handled_at - make_interval(secs => @seconds)
                     WHERE event_id = @id;
                     """;

                var id_ = command.CreateParameter();
                id_.ParameterName = "id";
                id_.Value = id;
                command.Parameters.Add(id_);

                var seconds = command.CreateParameter();
                seconds.ParameterName = "seconds";
                seconds.Value = by.TotalSeconds;
                command.Parameters.Add(seconds);

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
