using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Hérite de BaseHostTest : le runner ouvre ses propres transactions Npgsql,
// qui ne survivraient pas sous le TransactionScope ambiant de
// BaseIntegrationTest — même contrainte que les migrations EF (voir TestHost).
// Les sondes écrivent pour de vrai, d'où le nettoyage explicite.
[TestFixture]
[TestOf(typeof(DataMigrationRunner))]
public sealed class DataMigrationRunnerTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [SetUp]
    public async Task SetUp()
    {
        ProbeRecordingDataMigration.LastDispatcherType = null;
        await CleanUpProbeTracesAsync();
    }

    [TearDown]
    public async Task TearDown() => await CleanUpProbeTracesAsync();

    [Test]
    public async Task ApplyAsync_ShouldApplyAndJournal_WhenTheMigrationSucceeds()
    {
        // Arrange

        await using var runner = DataMigrationRunner.Create(
            services: Factory.Services,
            dbContextType: typeof(ProbeDbContext)
        );

        // Act

        await runner.ApplyAsync(
            migrationType: typeof(ProbeRecordingDataMigration),
            cancellationToken: CancellationToken.None
        );

        // Assert

        (await CountRowsWithLabelAsync(ProbeRecordingDataMigration.ProbeLabel)).Should().Be(1);

        var applied = await runner.GetAppliedIdsAsync(CancellationToken.None);

        applied.Should().Contain("99999999999901_ProbeRecordingDataMigration");
    }

    [Test]
    public async Task ApplyAsync_ShouldLeaveNoTrace_WhenTheMigrationFails()
    {
        // Arrange

        await using var runner = DataMigrationRunner.Create(
            services: Factory.Services,
            dbContextType: typeof(ProbeDbContext)
        );

        // Act

        var act = () => runner.ApplyAsync(
            migrationType: typeof(ProbeFailingDataMigration),
            cancellationToken: CancellationToken.None
        );

        // Assert

        await act.Should().ThrowAsync<InvalidOperationException>();

        // La sonde avait écrit sa ligne avant de lever : son absence prouve que
        // la migration entière — SQL de bordure compris — vit dans la
        // transaction du runner, et le journal vierge que la reprise rejouera
        // cette migration au prochain run.
        (await CountRowsWithLabelAsync(ProbeFailingDataMigration.ProbeLabel)).Should().Be(0);

        var applied = await runner.GetAppliedIdsAsync(CancellationToken.None);

        applied.Should().NotContain("99999999999902_ProbeFailingDataMigration");
    }

    [Test]
    public async Task Create_ShouldNeuterTheDomainEventDispatcher_WhenAMigrationResolvesIt()
    {
        // Arrange

        await using var runner = DataMigrationRunner.Create(
            services: Factory.Services,
            dbContextType: typeof(ProbeDbContext)
        );

        // Act

        await runner.ApplyAsync(
            migrationType: typeof(ProbeRecordingDataMigration),
            cancellationToken: CancellationToken.None
        );

        // Assert

        // Une migration re-représente des faits déjà établis : rien ne doit
        // dispatcher dans son scope — ni elle, ni le SaveChangesAsync du
        // DbContext qu'elle partage avec le runner (ADR 0013).
        ProbeRecordingDataMigration.LastDispatcherType.Should().Be(typeof(NoOpDomainEventDispatcher));
    }

    private static async Task<long> CountRowsWithLabelAsync(string label)
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = $"SELECT count(*) FROM {dbContext.Schema}.probe_things WHERE label = @label";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "label";
            parameter.Value = label;
            command.Parameters.Add(parameter);

            return (long)(await command.ExecuteScalarAsync())!;
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task CleanUpProbeTracesAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText =
                $"""
                 DELETE FROM {dbContext.Schema}.probe_things WHERE label LIKE 'probe-data-migration%';
                 DELETE FROM {dbContext.Schema}.__data_migrations_history WHERE migration_id LIKE '999999999999%';
                 """;

            await command.ExecuteNonQueryAsync();
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
