using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.Ledger.Infrastructure.Persistence.DataMigrations;
using LoreBank.Ledger.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Fakes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Ledger.Test.Infrastructure.Persistence.DataMigrations;

// Le rejeu du maillon central du triptyque (ADR 0013) : la migration lit une
// forme intermédiaire — la colonne encore nullable — que la timeline complète
// du conteneur ne connaît plus. La sonde la recrée le temps du rejeu et
// rétablit la contrainte dans un finally : c'est le prix d'un backfill prouvé
// sur une base migrée jusqu'au bout, et il ne se paie plus en TearDown qu'il
// faut penser à écrire. Hérite de BaseHostTest : la migration écrit pour de
// vrai.
[TestFixture]
[TestOf(typeof(BackfillJournalEntryRecordedAt))]
public sealed class BackfillJournalEntryRecordedAtTest : BaseHostTest<LedgerWebAppFactory>
{
    private readonly static Guid UndatedId = Guid.Parse("00000000-0000-0000-0000-00000000a001");

    private readonly static Guid WitnessId = Guid.Parse("00000000-0000-0000-0000-00000000a002");

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
    public async Task ExecuteAsync_ShouldDateTheEntryWithTheMigrationInstant_WhenARowPredatesTheColumn()
    {
        // Act

        await WithUndatedColumnAsync(async () =>
            {
                await InsertEntryAsync(
                    id: UndatedId,
                    recordedAt: null
                );

                await ReplayAsync();
            }
        );

        // Assert

        (await ReadRecordedAtAsync(UndatedId)).Should().Be(MigrationInstant);
    }

    [Test]
    public async Task ExecuteAsync_ShouldLeaveTheRowUntouched_WhenTheEntryIsAlreadyDated()
    {
        // Act

        await WithUndatedColumnAsync(async () =>
            {
                await InsertEntryAsync(
                    id: WitnessId,
                    recordedAt: WitnessInstant
                );

                await ReplayAsync();
            }
        );

        // Assert

        (await ReadRecordedAtAsync(WitnessId)).Should().Be(WitnessInstant);
    }

    // Le relâchement tient le temps du rejeu seulement : à la sortie, le
    // backfill a daté toute ligne nue, donc la contrainte se repose.
    private static Task WithUndatedColumnAsync(Func<Task> action) =>
        DataMigrationProbe<LedgerDbContext>.WithNullableColumnAsync(
            factory: Factory,
            table: "journal_entries",
            column: "recorded_at",
            action: action
        );

    private static Task ReplayAsync() =>
        DataMigrationProbe<LedgerDbContext>.ReplayAsync(
            factory: Factory,
            migrationFor: dbContext => new BackfillJournalEntryRecordedAt(
                context: dbContext,
                timeProvider: new ConfigurableTimeProvider { Instant = MigrationInstant }
            )
        );

    private static Task InsertEntryAsync(
        Guid id,
        DateTimeOffset? recordedAt
    ) =>
        DataMigrationProbe<LedgerDbContext>.ExecuteAsync(
            factory: Factory,
            sqlFor: schema => $"INSERT INTO {schema}.journal_entries (id, version, recorded_at) "
            + "VALUES (@id, 0, @recordedAt)",
            parameters: new Dictionary<string, object> {
                ["id"] = id,
                ["recordedAt"] = recordedAt.HasValue ? recordedAt.Value : DBNull.Value,
            }
        );

    private static Task<DateTimeOffset?> ReadRecordedAtAsync(Guid id) =>
        DataMigrationProbe<LedgerDbContext>.ReadAsync<DateTimeOffset?>(
            factory: Factory,
            sqlFor: schema => $"SELECT recorded_at FROM {schema}.journal_entries WHERE id = @id",
            parameters: new Dictionary<string, object> {
                ["id"] = id,
            }
        );

    private static Task DeleteArrangedRowsAsync() =>
        DataMigrationProbe<LedgerDbContext>.ExecuteAsync(
            factory: Factory,
            sqlFor: schema => $"DELETE FROM {schema}.journal_entries WHERE id IN (@undated, @witness)",
            parameters: new Dictionary<string, object> {
                ["undated"] = UndatedId,
                ["witness"] = WitnessId,
            }
        );
}
