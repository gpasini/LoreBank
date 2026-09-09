using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.Bank.Infrastructure.Persistence.DataMigrations;

// Le maillon central du triptyque ajouter / backfiller / resserrer (ADR 0013)
// pour l'Instant d'ouverture (ADR 0024) : les comptes nés avant la colonne
// n'ont aucune date connue — ils reçoivent l'Instant de la migration, demandé
// à TimeProvider comme le ferait une Application, jamais à l'horloge. Le SQL
// touche une forme intermédiaire (la colonne encore nullable) que le modèle
// vivant ne matérialise plus : c'est le SQL de bordure permis. Une fois
// appliquée partout, cette classe est un artefact mort, supprimable avec sa
// ligne de journal.
[DataMigration("20260909084120")]
public sealed class BackfillBankAccountOpenedAt(
    BankDbContext context,
    TimeProvider timeProvider
) : DataMigration(context)
{
    public override Task ExecuteAsync(CancellationToken cancellationToken) => ExecuteSqlAsync(
        sql: $"UPDATE {Schema}.bank_accounts SET opened_at = @openedAt WHERE opened_at IS NULL",
        parameters: new Dictionary<string, object> {
            ["openedAt"] = timeProvider.GetUtcNow(),
        },
        cancellationToken: cancellationToken
    );
}
