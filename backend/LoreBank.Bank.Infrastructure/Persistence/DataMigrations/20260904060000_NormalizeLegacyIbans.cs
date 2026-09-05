using LoreBank.SharedKernel.Domain.ValueObjects;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.Bank.Infrastructure.Persistence.DataMigrations;

// L'exemple de référence d'une migration de données (ADR 0013) : des IBAN
// écrits avant le socle (import initial) peuvent porter espaces et
// minuscules. La règle de normalisation n'est pas recopiée en SQL — c'est le
// VO vivant qui la porte : la migration lit la forme brute, laisse Iban
// normaliser, et ne réécrit que les lignes qui changent. Une fois appliquée
// sur tous les environnements, cette classe est un artefact mort, supprimable
// avec sa ligne de journal.
[DataMigration("20260904060000")]
public sealed class NormalizeLegacyIbans(BankDbContext context) : DataMigration(context)
{
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var accounts = await QueryAsync(
            sql: $"SELECT id, iban FROM {Schema}.bank_accounts",
            parameters: new Dictionary<string, object>(),
            map: reader => new {
                Id = reader.GetGuid(0),
                RawIban = reader.GetString(1),
            },
            cancellationToken: cancellationToken
        );

        foreach (var account in accounts) {
            var canonicalIban = Iban.Parse(account.RawIban).Value;

            if (canonicalIban == account.RawIban) {
                continue;
            }

            await ExecuteSqlAsync(
                sql: $"UPDATE {Schema}.bank_accounts SET iban = @iban WHERE id = @id",
                parameters: new Dictionary<string, object> {
                    ["iban"] = canonicalIban,
                    ["id"] = account.Id,
                },
                cancellationToken: cancellationToken
            );
        }
    }
}
