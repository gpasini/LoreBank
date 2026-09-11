using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Les gestes étendus des tests d'outbox du socle — rejeu, inbox, poison —
// sur le terrain probe. Étendus et internes à dessein : un module publieur
// n'a que la surface publique d'OutboxProbe, ces gestes-ci re-prouveraient
// des invariants du socle.
//
// Ce qui se lit passe par le store (une ligne d'outbox n'a qu'un endroit qui
// sache comment elle est faite) ; ce qui se manipule reste du SQL de sonde —
// simuler un crash, une autre instance ou le temps qui passe n'est pas un
// geste de production, et le store n'a pas à porter de verbe pour ça. Les
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
        bool Reserved,
        string? TraceParent
    );

    internal static Task CleanAsync(IntegrationTestWebAppFactory factory) =>
        ExecuteAsync(
            factory: factory,
            sql: dbContext =>
                $"""
                 DELETE FROM {Inbox.TableOf(dbContext)} WHERE handler LIKE '%Probe%';
                 DELETE FROM {Outbox.TableOf(dbContext)} WHERE discriminant LIKE '%probe-%';
                 """,
            parameters: new Dictionary<string, object>()
        );

    internal static async Task<Row?> FindRowAsync(
        IntegrationTestWebAppFactory factory,
        string discriminant
    )
    {
        var rows = await ProbeSql.OnOutboxAsync(
            factory: factory,
            action: outbox => outbox.ReadAllAsync(CancellationToken.None)
        );

        return rows
            .Where(row => row.Discriminant == discriminant)
            .Select(row => new Row(
                    Id: row.Id,
                    Attempts: row.Attempts,
                    Dispatched: row.Dispatched,
                    Poisoned: row.Poisoned,
                    LastError: row.LastError,
                    Reserved: row.Reserved,
                    TraceParent: row.TraceParent
                )
            )
            .FirstOrDefault();
    }

    internal static Task<long> CountInboxAsync(
        IntegrationTestWebAppFactory factory,
        Guid eventId
    ) =>
        OutboxProbe.OnDbContextAsync<ProbeDbContext, long>(
            factory: factory,
            action: dbContext => ModuleSql.ExecuteAsync(
                dbContext: dbContext,
                sql: $"SELECT count(*) FROM {Inbox.TableOf(dbContext)} WHERE event_id = @eventId",
                parameters: new Dictionary<string, object> {
                    ["eventId"] = eventId,
                },
                execute: async (
                    command,
                    token
                ) => (long) (await command.ExecuteScalarAsync(token))!,
                cancellationToken: CancellationToken.None
            )
        );

    // Simule la redélivrance : la ligne redevient pending, comme après un
    // crash tombé entre le commit des handlers et le marquage de l'outbox.
    internal static Task RedeliverAsync(
        IntegrationTestWebAppFactory factory,
        Guid id
    ) =>
        ExecuteAsync(
            factory: factory,
            sql: dbContext => $"UPDATE {Outbox.TableOf(dbContext)} SET dispatched_at = NULL WHERE id = @id",
            parameters: new Dictionary<string, object> {
                ["id"] = id,
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
            sql: dbContext =>
                $"""
                 UPDATE {Outbox.TableOf(dbContext)}
                 SET reserved_until = now() + make_interval(secs => @seconds)
                 WHERE id = @id
                 """,
            parameters: new Dictionary<string, object> {
                ["id"] = id,
                ["seconds"] = fromNow.TotalSeconds,
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
            sql: dbContext =>
                $"""
                 UPDATE {Outbox.TableOf(dbContext)}
                 SET occurred_at = occurred_at - make_interval(secs => @seconds),
                     next_attempt_at = next_attempt_at - make_interval(secs => @seconds),
                     dispatched_at = dispatched_at - make_interval(secs => @seconds),
                     poisoned_at = poisoned_at - make_interval(secs => @seconds)
                 WHERE id = @id;
                 UPDATE {Inbox.TableOf(dbContext)}
                 SET handled_at = handled_at - make_interval(secs => @seconds)
                 WHERE event_id = @id;
                 """,
            parameters: new Dictionary<string, object> {
                ["id"] = id,
                ["seconds"] = by.TotalSeconds,
            }
        );

    private static Task ExecuteAsync(
        IntegrationTestWebAppFactory factory,
        Func<ProbeDbContext, string> sql,
        IReadOnlyDictionary<string, object> parameters
    ) =>
        OutboxProbe.OnDbContextAsync<ProbeDbContext, int>(
            factory: factory,
            action: dbContext => ModuleSql.ExecuteNonQueryAsync(
                dbContext: dbContext,
                sql: sql(dbContext),
                parameters: parameters,
                cancellationToken: CancellationToken.None
            )
        );
}
