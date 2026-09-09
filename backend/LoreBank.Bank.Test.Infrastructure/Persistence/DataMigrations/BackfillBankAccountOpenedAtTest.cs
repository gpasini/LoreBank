using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Infrastructure.Persistence.DataMigrations;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Fakes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Persistence.DataMigrations;

// Le rejeu du maillon central du triptyque (ADR 0013) : la migration lit une
// forme intermédiaire — la colonne encore nullable — que la timeline complète
// du conteneur ne connaît plus. Le test la recrée le temps de la fixture
// (DROP NOT NULL au SetUp, SET NOT NULL au TearDown, lignes arrangées
// supprimées avant) : c'est le prix d'un backfill prouvé sur une base migrée
// jusqu'au bout. Hérite de BaseHostTest : la migration écrit pour de vrai.
[TestFixture]
[TestOf(typeof(BackfillBankAccountOpenedAt))]
public sealed class BackfillBankAccountOpenedAtTest : BaseHostTest<BankWebAppFactory>
{
    private const string UndatedIban = "DE89370400440532013000";

    private const string WitnessIban = "ES9121000418450200051332";

    private static readonly DateTimeOffset MigrationInstant = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 8,
        minute: 30,
        second: 0,
        offset: TimeSpan.Zero
    );

    private static readonly DateTimeOffset WitnessInstant = new(
        year: 2025,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    [SetUp]
    public async Task SetUp()
    {
        await DeleteArrangedRowsAsync();
        await ExecuteRawSqlAsync(
            sqlFor: dbContext => $"ALTER TABLE {dbContext.Schema}.bank_accounts ALTER COLUMN opened_at DROP NOT NULL",
            parameters: []
        );
    }

    [TearDown]
    public async Task TearDown()
    {
        await DeleteArrangedRowsAsync();
        await ExecuteRawSqlAsync(
            sqlFor: dbContext => $"ALTER TABLE {dbContext.Schema}.bank_accounts ALTER COLUMN opened_at SET NOT NULL",
            parameters: []
        );
    }

    [Test]
    public async Task ExecuteAsync_ShouldDateTheAccountWithTheMigrationInstant_WhenARowPredatesTheColumn()
    {
        // Arrange

        var undatedId = Guid.NewGuid();

        await InsertAccountAsync(
            id: undatedId,
            iban: UndatedIban,
            openedAt: null
        );

        // Act

        await RunMigrationAsync();

        // Assert

        (await ReadOpenedAtAsync(undatedId)).Should().Be(MigrationInstant);
    }

    [Test]
    public async Task ExecuteAsync_ShouldLeaveTheRowUntouched_WhenTheAccountIsAlreadyDated()
    {
        // Arrange

        var witnessId = Guid.NewGuid();

        await InsertAccountAsync(
            id: witnessId,
            iban: WitnessIban,
            openedAt: WitnessInstant
        );

        // Act

        await RunMigrationAsync();

        // Assert

        (await ReadOpenedAtAsync(witnessId)).Should().Be(WitnessInstant);
    }

    private static async Task RunMigrationAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        await new BackfillBankAccountOpenedAt(
            context: dbContext,
            timeProvider: new ConfigurableTimeProvider { Instant = MigrationInstant }
        ).ExecuteAsync(CancellationToken.None);
    }

    private static async Task InsertAccountAsync(
        Guid id,
        string iban,
        DateTimeOffset? openedAt
    ) => await ExecuteRawSqlAsync(
        sqlFor: dbContext => $"INSERT INTO {dbContext.Schema}.bank_accounts (id, iban, balance_amount, balance_currency, is_closed, opened_at) "
                             + "VALUES (@id, @iban, 0, 'EUR', false, @openedAt)",
        parameters: new Dictionary<string, object> {
            ["id"] = id,
            ["iban"] = iban,
            ["openedAt"] = openedAt.HasValue ? openedAt.Value : DBNull.Value,
        }
    );

    private static async Task<DateTimeOffset?> ReadOpenedAtAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = $"SELECT opened_at FROM {dbContext.Schema}.bank_accounts WHERE id = @id";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = id;
            command.Parameters.Add(parameter);

            var value = await command.ExecuteScalarAsync();

            return value is DateTime dateTime ? new DateTimeOffset(dateTime) : null;
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task DeleteArrangedRowsAsync() => await ExecuteRawSqlAsync(
        sqlFor: dbContext => $"DELETE FROM {dbContext.Schema}.bank_accounts WHERE iban IN (@undated, @witness)",
        parameters: new Dictionary<string, object> {
            ["undated"] = UndatedIban,
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
