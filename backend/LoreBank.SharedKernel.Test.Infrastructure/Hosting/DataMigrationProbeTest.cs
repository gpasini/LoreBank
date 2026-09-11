using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le seul geste de la sonde qui ait un comportement à lui : relâcher une
// contrainte le temps d'une action, et la rétablir quoi qu'il arrive. C'est
// le maillon central du triptyque d'ADR 0013 — un backfill lit une forme
// intermédiaire que la base migrée jusqu'au bout ne connaît plus — et c'est
// le seul endroit du repo qui casse volontairement le schéma.
[TestFixture]
[TestOf(typeof(DataMigrationProbe<>))]
public sealed class DataMigrationProbeTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [Test]
    public async Task WithNullableColumnAsync_ShouldLetTheActionSeeTheColumnNullable_WhenItRuns()
    {
        // Arrange

        var id = Guid.NewGuid();
        var nullableInside = false;

        // Act

        await DataMigrationProbe<ProbeDbContext>.WithNullableColumnAsync(
            factory: Factory,
            table: "probe_things",
            column: "label",
            action: async () =>
            {
                // Une ligne sans label : impossible hors du relâchement.
                await DataMigrationProbe<ProbeDbContext>.ExecuteAsync(
                    factory: Factory,
                    sqlFor: schema => $"INSERT INTO {schema}.probe_things (id, label, kind, active) "
                    + "VALUES (@id, NULL, 'probe', true)",
                    parameters: new Dictionary<string, object> {
                        ["id"] = id,
                    }
                );

                nullableInside = true;

                // Le rétablissement suppose qu'aucun NULL ne reste — c'est ce
                // qu'un backfill garantit, et ce que son test affirme.
                await DataMigrationProbe<ProbeDbContext>.ExecuteAsync(
                    factory: Factory,
                    sqlFor: schema => $"DELETE FROM {schema}.probe_things WHERE id = @id",
                    parameters: new Dictionary<string, object> {
                        ["id"] = id,
                    }
                );
            }
        );

        // Assert

        nullableInside.Should().BeTrue();
        (await IsLabelNullableAsync()).Should().BeFalse();
    }

    [Test]
    public async Task WithNullableColumnAsync_ShouldRestoreTheConstraint_WhenTheActionThrows()
    {
        // Act

        var act = () => DataMigrationProbe<ProbeDbContext>.WithNullableColumnAsync(
            factory: Factory,
            table: "probe_things",
            column: "label",
            action: () => throw new InvalidOperationException("le rejeu a échoué")
        );

        // Assert — l'échec passe, et la base ne reste pas relâchée derrière lui.

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("le rejeu a échoué");

        (await IsLabelNullableAsync()).Should().BeFalse();
    }

    private static async Task<bool> IsLabelNullableAsync() =>
        await ProbeSql.CountAsync(
            factory: Factory,
            sql: """
                 SELECT count(*) FROM information_schema.columns
                 WHERE table_schema = 'probe' AND table_name = 'probe_things'
                   AND column_name = 'label' AND is_nullable = 'YES'
                 """
        ) > 0;
}
