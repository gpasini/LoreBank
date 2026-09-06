using System.Data;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Persistence;

// Le geste SQL unique du socle, épinglé une fois pour ses quatre canaux
// (readers, migrations de données, runner, outbox/inbox) : emprunt compté
// par EF, refermeture dans le finally, clés de paramètres nues, et
// enrôlement dans la transaction EF courante — la nuance qui divergeait
// entre les copies avant l'unification.
[TestFixture]
[TestOf(typeof(ModuleSql))]
public sealed class ModuleSqlTest
{
    private SqliteConnection _connection = null!;

    private TestModuleDbContext _context = null!;

    [SetUp]
    public async Task OpenConnection()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _context = new TestModuleDbContext(
            options: new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite(_connection).Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );
        await _context.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task CloseConnection()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // L'emprunt est refcompté par EF : ModuleSql ne ferme pas une connexion
    // qu'il n'a pas ouverte — c'est ce qui le rend sûr sous une commande dont
    // EF tient déjà la connexion.
    [Test]
    public async Task ExecuteAsync_ShouldLeaveTheConnectionOpen_WhenEfAlreadyHeldIt()
    {
        // Arrange

        await _context.Database.OpenConnectionAsync();

        // Act

        await CountThingsAsync(_context);

        // Assert

        _context.Database.GetDbConnection().State.Should().Be(ConnectionState.Open);

        await _context.Database.CloseConnectionAsync();
    }

    // Ici la connexion appartient à EF (chaîne de connexion, pas de connexion
    // fournie) : c'est le seul cas où CloseConnectionAsync ferme réellement —
    // le finally doit relâcher l'emprunt même quand le SQL lève.
    [Test]
    public async Task ExecuteAsync_ShouldReleaseTheBorrowedConnection_WhenTheSqlFails()
    {
        // Arrange

        await using var context = new TestModuleDbContext(
            options: new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite("DataSource=:memory:").Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );

        // Act

        var act = () => ModuleSql.ExecuteNonQueryAsync(
            dbContext: context,
            sql: "DELETE FROM nowhere",
            parameters: new Dictionary<string, object>(),
            cancellationToken: CancellationToken.None
        );

        // Assert

        await act.Should().ThrowAsync<SqliteException>();
        context.Database.GetDbConnection().State.Should().Be(ConnectionState.Closed);
    }

    // Le SQL nomme @id, la clé du dictionnaire est nue : l'asymétrie est un
    // contrat — une clé préfixée doublerait le @ et casserait le binding.
    [Test]
    public async Task ExecuteAsync_ShouldBindBareParameterKeys()
    {
        // Arrange

        var id = Guid.NewGuid();

        // Act

        var inserted = await ModuleSql.ExecuteNonQueryAsync(
            dbContext: _context,
            sql: """INSERT INTO "Things" ("Id") VALUES (@id)""",
            parameters: new Dictionary<string, object> { ["id"] = id },
            cancellationToken: CancellationToken.None
        );

        // Assert

        inserted.Should().Be(1);
        (await CountThingsAsync(_context)).Should().Be(1);
    }

    // L'enrôlement dans la transaction EF courante : indispensable au
    // tout-ou-rien du DataMigrationRunner, no-op quand il n'y a pas de
    // transaction explicite. Le rollback qui emporte l'écriture prouve que la
    // commande était bien dedans, pas à côté.
    [Test]
    public async Task ExecuteAsync_ShouldEnlistInTheCurrentTransaction_WhenOneIsOpen()
    {
        // Act

        await using (var transaction = await _context.Database.BeginTransactionAsync()) {
            await ModuleSql.ExecuteNonQueryAsync(
                dbContext: _context,
                sql: """INSERT INTO "Things" ("Id") VALUES (@id)""",
                parameters: new Dictionary<string, object> { ["id"] = Guid.NewGuid() },
                cancellationToken: CancellationToken.None
            );

            await transaction.RollbackAsync();
        }

        // Assert

        (await CountThingsAsync(_context)).Should().Be(0);
    }

    private static Task<long> CountThingsAsync(TestModuleDbContext context) =>
        ModuleSql.ExecuteAsync(
            dbContext: context,
            sql: """SELECT count(*) FROM "Things" """,
            parameters: new Dictionary<string, object>(),
            execute: async (
                command,
                token
            ) => (long)(await command.ExecuteScalarAsync(token))!,
            cancellationToken: CancellationToken.None
        );
}
