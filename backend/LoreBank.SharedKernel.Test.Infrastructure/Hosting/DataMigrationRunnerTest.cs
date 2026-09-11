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
        ProbeRecordingDataMigration.LastDispatcherType.Should().Be<NoOpDomainEventDispatcher>();
    }

    // Le geste SQL du socle, par ProbeSql : le test du runner prouve le
    // tout-ou-rien, pas une migration — il n'a rien à faire de la sonde de
    // rejeu.
    private static Task<long> CountRowsWithLabelAsync(string label) =>
        ProbeSql.CountAsync(
            factory: Factory,
            sql: "SELECT count(*) FROM probe.probe_things WHERE label = @label",
            parameters: new Dictionary<string, object> {
                ["label"] = label,
            }
        );

    private static Task CleanUpProbeTracesAsync() =>
        ProbeSql.ExecuteAsync(
            factory: Factory,
            sql: """
                 DELETE FROM probe.probe_things WHERE label LIKE 'probe-data-migration%';
                 DELETE FROM probe.__data_migrations_history WHERE migration_id LIKE '999999999999%';
                 """
        );
}
