using System.Data.Common;
using LoreBank.Bank.Contracts.Readers;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Readers;

namespace LoreBank.Bank.Infrastructure.Readers;

// L'implémentation du port de lecture publié : même discipline que
// BankAccountReader — la table, pas l'agrégat, et seulement les colonnes du
// DTO. Le lien colonne → propriété n'est vérifié par aucun compilateur :
// BankAccountsContractReaderTest relit chaque champ après écriture.
public sealed class BankAccountsContractReader(BankDbContext context)
    : ModuleReader(context), IBankAccountsContract
{
    private string SelectById =>
        $"""
        SELECT id, iban
        FROM {Schema}.bank_accounts
        WHERE id = @id
        """;

    public Task<BankAccountSummary?> FindByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken
    ) => QuerySingleOrDefaultAsync(
        sql: SelectById,
        parameters: new() { ["id"] = accountId },
        map: Map,
        cancellationToken: cancellationToken
    );

    private static BankAccountSummary Map(DbDataReader reader) => new(
        Id: reader.GetGuid(0),
        Iban: reader.GetString(1)
    );
}
