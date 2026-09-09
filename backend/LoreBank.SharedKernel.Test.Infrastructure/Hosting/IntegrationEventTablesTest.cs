using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Les tables du socle n'ont pas de timeline de migration : leur DDL est
// idempotent et ne fait que s'ajouter (ADR 0021). Ce test rejoue EnsureAsync
// sur une base qui porte l'ancienne forme des tables — arrangée en SQL brut,
// comme les tests de data migration — et prouve que ce qui est venu après
// rattrape l'existant.
[TestFixture]
[TestOf(typeof(IntegrationEventTables))]
public sealed class IntegrationEventTablesTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [Test]
    public async Task EnsureAsync_ShouldAddWhatCameLater_WhenTheTablesHaveTheirOriginalShape()
    {
        // Arrange — la forme d'origine : ni bail, ni traceparent, ni index de
        // purge.

        await ExecuteAsync(
            """
            ALTER TABLE probe.__outbox DROP COLUMN IF EXISTS reserved_until;
            ALTER TABLE probe.__outbox DROP COLUMN IF EXISTS trace_parent;
            DROP INDEX IF EXISTS probe.ix___outbox_dispatched;
            DROP INDEX IF EXISTS probe.ix___inbox_handled;
            """
        );

        // Act

        using (var scope = Factory.Services.CreateScope()) {
            await IntegrationEventTables.EnsureAsync(
                dbContext: scope.ServiceProvider.GetRequiredService<ProbeDbContext>(),
                cancellationToken: CancellationToken.None
            );
        }

        // Assert

        (await CountAsync(
            """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'probe' AND table_name = '__outbox'
              AND column_name IN ('reserved_until', 'trace_parent')
            """
        )).Should().Be(2);

        (await CountAsync(
            """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'probe' AND indexname IN ('ix___outbox_dispatched', 'ix___inbox_handled')
            """
        )).Should().Be(2);
    }

    private static Task<int> ExecuteAsync(string sql) =>
        OutboxProbe.ExecuteAsync(
            factory: Factory,
            action: (
                ProbeDbContext _,
                System.Data.Common.DbCommand command
            ) => {
                command.CommandText = sql;

                return command.ExecuteNonQueryAsync();
            }
        );

    private static Task<long> CountAsync(string sql) =>
        OutboxProbe.ExecuteAsync(
            factory: Factory,
            action: async (
                ProbeDbContext _,
                System.Data.Common.DbCommand command
            ) => {
                command.CommandText = sql;

                return (long)(await command.ExecuteScalarAsync())!;
            }
        );
}
