using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La sonde d'échec : écrit une ligne PUIS lève — c'est l'écart entre ces deux
// gestes que le test observe pour prouver le tout-ou-rien du runner.
[DataMigration("99999999999902")]
public sealed class ProbeFailingDataMigration(ProbeDbContext context) : DataMigration(context)
{
    public const string ProbeLabel = "probe-data-migration-0002";

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await ExecuteSqlAsync(
            sql: $"INSERT INTO {Schema}.probe_things (id, label) VALUES (@id, @label)",
            parameters: new Dictionary<string, object> {
                ["id"] = Guid.NewGuid(),
                ["label"] = ProbeLabel,
            },
            cancellationToken: cancellationToken
        );

        throw new InvalidOperationException("La sonde échoue après avoir écrit : la ligne ne doit pas survivre.");
    }
}
