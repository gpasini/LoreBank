using System.Data.Common;
using LoreBank.Bank.Application.Readers;
using LoreBank.Bank.Application.Results;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Readers;

namespace LoreBank.Bank.Infrastructure.Readers;

// Lit la table, pas l'agrégat : aucun value object n'est reconstruit, aucune
// entité n'est suivie par le change tracker, et le SELECT ne ramène que les
// colonnes du DTO. L'emprunt de connexion vit dans ModuleReader — ici il ne
// reste que le SQL, les paramètres et la lecture des colonnes.
//
// Le SQL est écrit à la main, donc le lien colonne → propriété n'est vérifié par
// aucun compilateur : c'est `GetBankAccountByIdTest` qui l'épingle, en relisant
// chaque champ après écriture. Une migration qui renomme une colonne y fait
// rougir la suite.
public sealed class BankAccountReader(BankDbContext context) : ModuleReader(context), IBankAccountReader
{
    private const string SelectById =
        """
        SELECT id, iban, balance_amount, balance_currency, is_closed
        FROM bank.bank_accounts
        WHERE id = @id
        """;

    public Task<BankAccountResult?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    ) => QuerySingleOrDefaultAsync(
        sql: SelectById,
        parameters: new() { ["id"] = id },
        map: Map,
        cancellationToken: cancellationToken
    );

    private static BankAccountResult Map(DbDataReader reader) => new(
        Id: reader.GetGuid(0),
        Iban: reader.GetString(1),
        Balance: reader.GetDecimal(2),
        Currency: reader.GetString(3),
        IsClosed: reader.GetBoolean(4)
    );
}
