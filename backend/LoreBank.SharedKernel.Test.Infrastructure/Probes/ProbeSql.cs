using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Les gestes du terrain probe, sur le geste SQL du socle : les stores tels
// qu'un test les construit — sur un DbContext nu, sans conteneur à monter —
// et le SQL brut que seules les assertions de DDL demandent
// (information_schema, pg_indexes).
internal static class ProbeSql
{
    internal static Task<T> OnOutboxAsync<T>(
        IntegrationTestWebAppFactory factory,
        Func<Outbox, Task<T>> action
    ) =>
        OutboxProbe.OnOutboxAsync<ProbeDbContext, T>(
            factory: factory,
            action: action
        );

    internal static Task<T> OnInboxAsync<T>(
        IntegrationTestWebAppFactory factory,
        Func<Inbox, Task<T>> action
    ) =>
        OutboxProbe.OnInboxAsync<ProbeDbContext, T>(
            factory: factory,
            action: action
        );

    internal static Task<int> ExecuteAsync(
        IntegrationTestWebAppFactory factory,
        string sql
    ) =>
        OutboxProbe.OnDbContextAsync<ProbeDbContext, int>(
            factory: factory,
            action: dbContext => ModuleSql.ExecuteNonQueryAsync(
                dbContext: dbContext,
                sql: sql,
                parameters: new Dictionary<string, object>(),
                cancellationToken: CancellationToken.None
            )
        );

    internal static Task<long> CountAsync(
        IntegrationTestWebAppFactory factory,
        string sql
    ) =>
        OutboxProbe.OnDbContextAsync<ProbeDbContext, long>(
            factory: factory,
            action: dbContext => ModuleSql.ExecuteAsync(
                dbContext: dbContext,
                sql: sql,
                parameters: new Dictionary<string, object>(),
                execute: async (
                    command,
                    token
                ) => (long) (await command.ExecuteScalarAsync(token))!,
                cancellationToken: CancellationToken.None
            )
        );
}
