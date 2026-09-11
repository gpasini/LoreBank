using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Infrastructure.Persistence.DataMigrations;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Fakes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Persistence.DataMigrations;

// Le rejeu du maillon central du triptyque (ADR 0013) : la migration lit une
// forme intermédiaire — la colonne encore nullable — que la timeline complète
// du conteneur ne connaît plus. La sonde la recrée le temps du rejeu et
// rétablit la contrainte dans un finally : c'est le prix d'un backfill prouvé
// sur une base migrée jusqu'au bout, et il ne se paie plus en TearDown qu'il
// faut penser à écrire. Hérite de BaseHostTest : la migration écrit pour de
// vrai.
[TestFixture]
[TestOf(typeof(BackfillBankAccountOpenedAt))]
public sealed class BackfillBankAccountOpenedAtTest : BaseHostTest<BankWebAppFactory>
{
    private const string UndatedIban = "DE89370400440532013000";

    private const string WitnessIban = "ES9121000418450200051332";

    private readonly static DateTimeOffset MigrationInstant = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 8,
        minute: 30,
        second: 0,
        offset: TimeSpan.Zero
    );

    private readonly static DateTimeOffset WitnessInstant = new(
        year: 2025,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    [SetUp]
    public async Task SetUp() => await DeleteArrangedRowsAsync();

    [TearDown]
    public async Task TearDown() => await DeleteArrangedRowsAsync();

    [Test]
    public async Task ExecuteAsync_ShouldDateTheAccountWithTheMigrationInstant_WhenARowPredatesTheColumn()
    {
        // Arrange

        var undatedId = Guid.NewGuid();

        // Act

        await WithUndatedColumnAsync(async () =>
            {
                await InsertAccountAsync(
                    id: undatedId,
                    iban: UndatedIban,
                    openedAt: null
                );

                await ReplayAsync();
            }
        );

        // Assert

        (await ReadOpenedAtAsync(undatedId)).Should().Be(MigrationInstant);
    }

    [Test]
    public async Task ExecuteAsync_ShouldLeaveTheRowUntouched_WhenTheAccountIsAlreadyDated()
    {
        // Arrange

        var witnessId = Guid.NewGuid();

        // Act

        await WithUndatedColumnAsync(async () =>
            {
                await InsertAccountAsync(
                    id: witnessId,
                    iban: WitnessIban,
                    openedAt: WitnessInstant
                );

                await ReplayAsync();
            }
        );

        // Assert

        (await ReadOpenedAtAsync(witnessId)).Should().Be(WitnessInstant);
    }

    // Le relâchement tient le temps du rejeu seulement : à la sortie, le
    // backfill a daté toute ligne nue, donc la contrainte se repose.
    private static Task WithUndatedColumnAsync(Func<Task> action) =>
        DataMigrationProbe<BankDbContext>.WithNullableColumnAsync(
            factory: Factory,
            table: "bank_accounts",
            column: "opened_at",
            action: action
        );

    private static Task ReplayAsync() =>
        DataMigrationProbe<BankDbContext>.ReplayAsync(
            factory: Factory,
            migrationFor: dbContext => new BackfillBankAccountOpenedAt(
                context: dbContext,
                timeProvider: new ConfigurableTimeProvider { Instant = MigrationInstant }
            )
        );

    private static Task InsertAccountAsync(
        Guid id,
        string iban,
        DateTimeOffset? openedAt
    ) =>
        DataMigrationProbe<BankDbContext>.ExecuteAsync(
            factory: Factory,
            sqlFor: schema =>
                $"INSERT INTO {schema}.bank_accounts (id, iban, balance_amount, balance_currency, is_closed, opened_at) "
                + "VALUES (@id, @iban, 0, 'EUR', false, @openedAt)",
            parameters: new Dictionary<string, object> {
                ["id"] = id,
                ["iban"] = iban,
                ["openedAt"] = openedAt.HasValue ? openedAt.Value : DBNull.Value,
            }
        );

    private static Task<DateTimeOffset?> ReadOpenedAtAsync(Guid id) =>
        DataMigrationProbe<BankDbContext>.ReadAsync<DateTimeOffset?>(
            factory: Factory,
            sqlFor: schema => $"SELECT opened_at FROM {schema}.bank_accounts WHERE id = @id",
            parameters: new Dictionary<string, object> {
                ["id"] = id,
            }
        );

    private static Task DeleteArrangedRowsAsync() =>
        DataMigrationProbe<BankDbContext>.ExecuteAsync(
            factory: Factory,
            sqlFor: schema => $"DELETE FROM {schema}.bank_accounts WHERE iban IN (@undated, @witness)",
            parameters: new Dictionary<string, object> {
                ["undated"] = UndatedIban,
                ["witness"] = WitnessIban,
            }
        );
}
