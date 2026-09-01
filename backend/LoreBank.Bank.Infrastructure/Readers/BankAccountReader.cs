using System.Data.Common;
using LoreBank.Bank.Application.Readers;
using LoreBank.Bank.Application.Results;
using LoreBank.Bank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoreBank.Bank.Infrastructure.Readers;

// Lit la table, pas l'agrégat : aucun value object n'est reconstruit, aucune
// entité n'est suivie par le change tracker, et le SELECT ne ramène que les
// colonnes du DTO.
//
// Le SQL est écrit à la main, donc le lien colonne → propriété n'est vérifié par
// aucun compilateur : c'est `GetBankAccountByIdTest` qui l'épingle, en relisant
// chaque champ après écriture. Une migration qui renomme une colonne y fait
// rougir la suite.
public sealed class BankAccountReader(BankDbContext context) : IBankAccountReader
{
    private const string SelectById =
        """
        SELECT id, iban, balance_amount, balance_currency, is_closed
        FROM bank.bank_accounts
        WHERE id = @id
        """;

    public async Task<BankAccountResult?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        // La connexion est empruntée au DbContext, jamais ouverte en propre : une
        // seconde connexion vers le même PostgreSQL sous le TransactionScope
        // ambiant d'une commande ferait enrôler un second connecteur, et la
        // transaction escaladerait en distribué — non supporté hors Windows.
        //
        // Open/CloseConnectionAsync sont comptés par EF : ils n'ouvrent ni ne
        // ferment rien si EF tient déjà la connexion.
        await context.Database.OpenConnectionAsync(cancellationToken);

        try {
            await using var command = ((NpgsqlConnection)context.Database.GetDbConnection()).CreateCommand();

            command.CommandText = SelectById;
            command.Parameters.AddWithValue(
                parameterName: "id",
                value: id
            );

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            return await reader.ReadAsync(cancellationToken)
                ? Map(reader)
                : null;
        }
        finally {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static BankAccountResult Map(DbDataReader reader) => new(
        Id: reader.GetGuid(0),
        Iban: reader.GetString(1),
        Balance: reader.GetDecimal(2),
        Currency: reader.GetString(3),
        IsClosed: reader.GetBoolean(4)
    );
}
