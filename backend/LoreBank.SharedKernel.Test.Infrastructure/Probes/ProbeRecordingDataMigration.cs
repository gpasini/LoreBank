using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La sonde des tests de DataMigrationRunner : écrit une ligne par le chemin
// SQL de bordure et enregistre le dispatcher que le scope lui injecte. Le
// module de référence sert de banc d'essai, comme dans ModuleCompositionTest —
// la sonde vit dans l'assembly de test, jamais découverte par le scan d'un
// module. Timestamp volontairement hors de toute timeline réelle.
[DataMigration("99999999999901")]
public sealed class ProbeRecordingDataMigration(
    BankDbContext context,
    IDomainEventDispatcher dispatcher
) : DataMigration(context)
{
    public const string ProbeIban = "ZZ00PROBEDATAMIGRATION0001";

    public static Type? LastDispatcherType { get; set; }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        LastDispatcherType = dispatcher.GetType();

        await ExecuteSqlAsync(
            sql: $"INSERT INTO {Schema}.bank_accounts (id, iban, balance_amount, balance_currency, is_closed) "
                 + "VALUES (@id, @iban, 0, 'EUR', false)",
            parameters: new Dictionary<string, object> {
                ["id"] = Guid.NewGuid(),
                ["iban"] = ProbeIban,
            },
            cancellationToken: cancellationToken
        );
    }
}
