using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Infrastructure.Persistence.DataMigrations;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Persistence.DataMigrations;

// Le pendant « migration de données » du test de reader : le lien
// colonne → logique n'est vérifié par aucun compilateur, chaque migration a
// donc un test qui la rejoue sur des données arrangées. Hérite de
// BaseHostTest : la migration écrit pour de vrai — les lignes arrangées ici ne
// peuvent pas passer par les use cases, c'est précisément leur raison d'être
// (le VO normaliserait à l'entrée).
[TestFixture]
[TestOf(typeof(NormalizeLegacyIbans))]
public sealed class NormalizeLegacyIbansTest : BaseHostTest<BankWebAppFactory>
{
    private const string LegacyIban = "de89 3704 0044 0532 0130 00";

    private const string CanonicalizedIban = "DE89370400440532013000";

    private const string WitnessIban = "ES9121000418450200051332";

    [SetUp]
    public async Task SetUp() => await DeleteArrangedRowsAsync();

    [TearDown]
    public async Task TearDown() => await DeleteArrangedRowsAsync();

    [Test]
    public async Task ExecuteAsync_ShouldCanonicalizeTheIban_WhenARowPredatesNormalization()
    {
        // Arrange

        var legacyId = Guid.NewGuid();

        await InsertAccountAsync(
            id: legacyId,
            iban: LegacyIban
        );

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        // Act

        await new NormalizeLegacyIbans(dbContext).ExecuteAsync(CancellationToken.None);

        // Assert

        (await ReadIbanAsync(legacyId)).Should().Be(CanonicalizedIban);
    }

    [Test]
    public async Task ExecuteAsync_ShouldLeaveTheRowUntouched_WhenTheIbanIsAlreadyCanonical()
    {
        // Arrange

        var witnessId = Guid.NewGuid();

        await InsertAccountAsync(
            id: witnessId,
            iban: WitnessIban
        );

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        // Act

        await new NormalizeLegacyIbans(dbContext).ExecuteAsync(CancellationToken.None);

        // Assert

        (await ReadIbanAsync(witnessId)).Should().Be(WitnessIban);
    }

    private static async Task InsertAccountAsync(
        Guid id,
        string iban
    ) => await ExecuteRawSqlAsync(
        sqlFor: dbContext => $"INSERT INTO {dbContext.Schema}.bank_accounts (id, iban, balance_amount, balance_currency, is_closed) "
                             + "VALUES (@id, @iban, 0, 'EUR', false)",
        parameters: new Dictionary<string, object> {
            ["id"] = id,
            ["iban"] = iban,
        }
    );

    private static async Task<string?> ReadIbanAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = $"SELECT iban FROM {dbContext.Schema}.bank_accounts WHERE id = @id";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = id;
            command.Parameters.Add(parameter);

            return (string?)await command.ExecuteScalarAsync();
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task DeleteArrangedRowsAsync() => await ExecuteRawSqlAsync(
        sqlFor: dbContext => $"DELETE FROM {dbContext.Schema}.bank_accounts WHERE iban IN (@legacy, @canonicalized, @witness)",
        parameters: new Dictionary<string, object> {
            ["legacy"] = LegacyIban,
            ["canonicalized"] = CanonicalizedIban,
            ["witness"] = WitnessIban,
        }
    );

    private static async Task ExecuteRawSqlAsync(
        Func<BankDbContext, string> sqlFor,
        Dictionary<string, object> parameters
    )
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = sqlFor(dbContext);

            foreach (var (name, value) in parameters) {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            await command.ExecuteNonQueryAsync();
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
