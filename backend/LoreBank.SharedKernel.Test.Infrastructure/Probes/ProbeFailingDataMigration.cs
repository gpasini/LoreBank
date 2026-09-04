using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La sonde d'échec : écrit une ligne PUIS lève — c'est l'écart entre ces deux
// gestes que le test observe pour prouver le tout-ou-rien du runner.
[DataMigration("99999999999902")]
public sealed class ProbeFailingDataMigration(BankDbContext context) : DataMigration(context)
{
    public const string ProbeIban = "ZZ00PROBEDATAMIGRATION0002";

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await ExecuteSqlAsync(
            sql: $"INSERT INTO {Schema}.bank_accounts (id, iban, balance_amount, balance_currency, is_closed) "
                 + "VALUES (@id, @iban, 0, 'EUR', false)",
            parameters: new Dictionary<string, object> {
                ["id"] = Guid.NewGuid(),
                ["iban"] = ProbeIban,
            },
            cancellationToken: cancellationToken
        );

        throw new InvalidOperationException("La sonde échoue après avoir écrit : la ligne ne doit pas survivre.");
    }
}
