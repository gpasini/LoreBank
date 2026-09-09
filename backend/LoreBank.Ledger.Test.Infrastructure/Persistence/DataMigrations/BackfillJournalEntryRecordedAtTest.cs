using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.Ledger.Infrastructure.Persistence.DataMigrations;
using LoreBank.Ledger.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Fakes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Ledger.Test.Infrastructure.Persistence.DataMigrations;

// Le rejeu du maillon central du triptyque (ADR 0013) : la migration lit une
// forme intermédiaire — la colonne encore nullable — que la timeline complète
// du conteneur ne connaît plus. Le test la recrée le temps de la fixture
// (DROP NOT NULL au SetUp, SET NOT NULL au TearDown, lignes arrangées
// supprimées avant) : c'est le prix d'un backfill prouvé sur une base migrée
// jusqu'au bout. Hérite de BaseHostTest : la migration écrit pour de vrai.
[TestFixture]
[TestOf(typeof(BackfillJournalEntryRecordedAt))]
public sealed class BackfillJournalEntryRecordedAtTest : BaseHostTest<LedgerWebAppFactory>
{
    private static readonly Guid UndatedId = Guid.Parse("00000000-0000-0000-0000-00000000a001");

    private static readonly Guid WitnessId = Guid.Parse("00000000-0000-0000-0000-00000000a002");

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
            sqlFor: dbContext => $"ALTER TABLE {dbContext.Schema}.journal_entries ALTER COLUMN recorded_at DROP NOT NULL",
            parameters: []
        );
    }

    [TearDown]
    public async Task TearDown()
    {
        await DeleteArrangedRowsAsync();
        await ExecuteRawSqlAsync(
            sqlFor: dbContext => $"ALTER TABLE {dbContext.Schema}.journal_entries ALTER COLUMN recorded_at SET NOT NULL",
            parameters: []
        );
    }

    [Test]
    public async Task ExecuteAsync_ShouldDateTheEntryWithTheMigrationInstant_WhenARowPredatesTheColumn()
    {
        // Arrange

        await InsertEntryAsync(
            id: UndatedId,
            recordedAt: null
        );

        // Act

        await RunMigrationAsync();

        // Assert

        (await ReadRecordedAtAsync(UndatedId)).Should().Be(MigrationInstant);
    }

    [Test]
    public async Task ExecuteAsync_ShouldLeaveTheRowUntouched_WhenTheEntryIsAlreadyDated()
    {
        // Arrange

        await InsertEntryAsync(
            id: WitnessId,
            recordedAt: WitnessInstant
        );

        // Act

        await RunMigrationAsync();

        // Assert

        (await ReadRecordedAtAsync(WitnessId)).Should().Be(WitnessInstant);
    }

    private static async Task RunMigrationAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        await new BackfillJournalEntryRecordedAt(
            context: dbContext,
            timeProvider: new ConfigurableTimeProvider { Instant = MigrationInstant }
        ).ExecuteAsync(CancellationToken.None);
    }

    private static async Task InsertEntryAsync(
        Guid id,
        DateTimeOffset? recordedAt
    ) => await ExecuteRawSqlAsync(
        sqlFor: dbContext => $"INSERT INTO {dbContext.Schema}.journal_entries (id, version, recorded_at) "
                             + "VALUES (@id, 0, @recordedAt)",
        parameters: new Dictionary<string, object> {
            ["id"] = id,
            ["recordedAt"] = recordedAt.HasValue ? recordedAt.Value : DBNull.Value,
        }
    );

    private static async Task<DateTimeOffset?> ReadRecordedAtAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = $"SELECT recorded_at FROM {dbContext.Schema}.journal_entries WHERE id = @id";

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
        sqlFor: dbContext => $"DELETE FROM {dbContext.Schema}.journal_entries WHERE id IN (@undated, @witness)",
        parameters: new Dictionary<string, object> {
            ["undated"] = UndatedId,
            ["witness"] = WitnessId,
        }
    );

    private static async Task ExecuteRawSqlAsync(
        Func<LedgerDbContext, string> sqlFor,
        Dictionary<string, object> parameters
    )
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

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
