using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.Ledger.Infrastructure.Persistence.DataMigrations;

// Le maillon central du triptyque ajouter / backfiller / resserrer (ADR 0013)
// pour l'Instant de comptabilisation (ADR 0024) : les écritures nées avant la
// colonne n'ont aucune date connue — elles reçoivent l'Instant de la
// migration, demandé à TimeProvider comme le ferait une Application, jamais à
// l'horloge. Le SQL touche une forme intermédiaire (la colonne encore
// nullable) que le modèle vivant ne matérialise plus : c'est le SQL de
// bordure permis. Une fois appliquée partout, cette classe est un artefact
// mort, supprimable avec sa ligne de journal.
[DataMigration("20260909084401")]
public sealed class BackfillJournalEntryRecordedAt(
    LedgerDbContext context,
    TimeProvider timeProvider
) : DataMigration(context)
{
    public override Task ExecuteAsync(CancellationToken cancellationToken) => ExecuteSqlAsync(
        sql: $"UPDATE {Schema}.journal_entries SET recorded_at = @recordedAt WHERE recorded_at IS NULL",
        parameters: new Dictionary<string, object> {
            ["recordedAt"] = timeProvider.GetUtcNow(),
        },
        cancellationToken: cancellationToken
    );
}
