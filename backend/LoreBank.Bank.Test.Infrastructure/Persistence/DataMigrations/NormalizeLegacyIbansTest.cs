using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Infrastructure.Persistence.DataMigrations;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Persistence.DataMigrations;

// Le pendant « migration de données » du test de reader : le lien
// colonne → logique n'est vérifié par aucun compilateur, chaque migration a
// donc un test qui la rejoue sur des données arrangées. Hérite de
// BaseHostTest : la migration écrit pour de vrai — les lignes arrangées ici ne
// peuvent pas passer par les use cases, c'est précisément leur raison d'être
// (le VO normaliserait à l'entrée). Arranger, rejouer, relire : les trois
// gestes viennent de la sonde du socle, aucune plomberie ADO ici.
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

        // Act

        await ReplayAsync();

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

        // Act

        await ReplayAsync();

        // Assert

        (await ReadIbanAsync(witnessId)).Should().Be(WitnessIban);
    }

    private static Task ReplayAsync() =>
        DataMigrationProbe<BankDbContext>.ReplayAsync(
            factory: Factory,
            migrationFor: dbContext => new NormalizeLegacyIbans(dbContext)
        );

    private static Task InsertAccountAsync(
        Guid id,
        string iban
    ) =>
        DataMigrationProbe<BankDbContext>.ExecuteAsync(
            factory: Factory,
            sqlFor: schema =>
                $"INSERT INTO {schema}.bank_accounts (id, iban, balance_amount, balance_currency, is_closed, opened_at) "
                + "VALUES (@id, @iban, 0, 'EUR', false, @openedAt)",
            parameters: new Dictionary<string, object> {
                ["id"] = id,
                ["iban"] = iban,
                ["openedAt"] = DateTimeOffset.UnixEpoch,
            }
        );

    private static Task<string?> ReadIbanAsync(Guid id) =>
        DataMigrationProbe<BankDbContext>.ReadAsync<string>(
            factory: Factory,
            sqlFor: schema => $"SELECT iban FROM {schema}.bank_accounts WHERE id = @id",
            parameters: new Dictionary<string, object> {
                ["id"] = id,
            }
        );

    private static Task DeleteArrangedRowsAsync() =>
        DataMigrationProbe<BankDbContext>.ExecuteAsync(
            factory: Factory,
            sqlFor: schema => $"DELETE FROM {schema}.bank_accounts WHERE iban IN (@legacy, @canonicalized, @witness)",
            parameters: new Dictionary<string, object> {
                ["legacy"] = LegacyIban,
                ["canonicalized"] = CanonicalizedIban,
                ["witness"] = WitnessIban,
            }
        );
}
