using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La sonde des tests de DataMigrationRunner : écrit une ligne par le chemin
// SQL de bordure et enregistre le dispatcher que le scope lui injecte. Le
// ProbeModule sert de banc d'essai (ADR 0017) — la sonde vit dans l'assembly
// de test, hors de l'assembly du DbContext : découverte par ModuleMigrator,
// elle entrerait dans la timeline du harnais. Timestamp volontairement hors
// de toute timeline réelle.
[DataMigration("99999999999901")]
public sealed class ProbeRecordingDataMigration(
    ProbeDbContext context,
    IDomainEventDispatcher dispatcher
) : DataMigration(context)
{
    public const string ProbeLabel = "probe-data-migration-0001";

    public static Type? LastDispatcherType { get; set; }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        LastDispatcherType = dispatcher.GetType();

        await ExecuteSqlAsync(
            sql: $"INSERT INTO {Schema}.probe_things (id, label) VALUES (@id, @label)",
            parameters: new Dictionary<string, object> {
                ["id"] = Guid.NewGuid(),
                ["label"] = ProbeLabel,
            },
            cancellationToken: cancellationToken
        );
    }
}
