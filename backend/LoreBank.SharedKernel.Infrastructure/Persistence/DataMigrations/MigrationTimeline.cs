namespace LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

// La fusion des deux flux pending — migrations de schéma EF et migrations de
// données — en une seule timeline triée par comparaison ordinale. C'est elle
// qui rend l'entrelacement réel : une migration de données s'exécute après la
// migration de schéma qu'elle suit et avant celle qui la suit (ADR 0013).
internal static class MigrationTimeline
{
    internal static IReadOnlyList<MigrationStep> Merge(
        IEnumerable<string> schemaMigrationIds,
        IEnumerable<string> dataMigrationIds
    ) => schemaMigrationIds
        .Select(id => new MigrationStep(
            Id: id,
            IsData: false
        ))
        .Concat(dataMigrationIds.Select(id => new MigrationStep(
            Id: id,
            IsData: true
        )))
        .OrderBy(
            keySelector: step => step.Id,
            comparer: StringComparer.Ordinal
        )
        .ToList();
}

internal sealed record MigrationStep(
    string Id,
    bool IsData
);
