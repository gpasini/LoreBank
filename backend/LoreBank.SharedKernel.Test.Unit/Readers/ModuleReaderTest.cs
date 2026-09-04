using System.Data;
using LoreBank.SharedKernel.Infrastructure.Readers;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Readers;

[TestFixture]
[TestOf(typeof(ModuleReader))]
public sealed class ModuleReaderTest
{
    private SqliteConnection _connection = null!;

    private TestModuleDbContext _context = null!;

    private TestThingReader _reader = null!;

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

        _reader = new TestThingReader(_context);
    }

    [TearDown]
    public async Task CloseConnection()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task QuerySingleOrDefaultAsync_ShouldMapTheRow_WhenItExists()
    {
        // Arrange

        var thing = new TestThing(Guid.NewGuid());
        _context.Things.Add(thing);
        await _context.SaveChangesAsync();

        // Act

        var row = await _reader.GetByIdAsync(
            id: thing.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert

        row.Should().Be(new TestThingRow(thing.Id));
    }

    [Test]
    public async Task QuerySingleOrDefaultAsync_ShouldReturnNull_WhenNoRowMatches()
    {
        // Act

        var row = await _reader.GetByIdAsync(
            id: Guid.NewGuid(),
            cancellationToken: CancellationToken.None
        );

        // Assert

        row.Should().BeNull();
    }

    // L'emprunt est refcompté par EF : la base ne ferme pas une connexion
    // qu'elle n'a pas ouverte — c'est ce qui la rend sûre sous une commande
    // dont EF tient déjà la connexion.
    [Test]
    public async Task QuerySingleOrDefaultAsync_ShouldLeaveTheConnectionOpen_WhenEfAlreadyHeldIt()
    {
        // Arrange

        await _context.Database.OpenConnectionAsync();

        // Act

        await _reader.GetByIdAsync(
            id: Guid.NewGuid(),
            cancellationToken: CancellationToken.None
        );

        // Assert

        _context.Database.GetDbConnection().State.Should().Be(ConnectionState.Open);

        await _context.Database.CloseConnectionAsync();
    }

    // Ici la connexion appartient à EF (chaîne de connexion, pas de connexion
    // fournie) : c'est le seul cas où CloseConnectionAsync ferme réellement —
    // sur la connexion externe du SetUp, EF ne ferme jamais ce qu'il ne
    // possède pas, et l'état resterait Open quoi que fasse le finally.
    [Test]
    public async Task QuerySingleOrDefaultAsync_ShouldReleaseTheBorrowedConnection_WhenTheQueryFails()
    {
        // Arrange

        await using var context = new TestModuleDbContext(
            options: new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite("DataSource=:memory:").Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );
        var reader = new TestThingReader(context);

        // Act

        var act = async () => await reader.FailAsync(CancellationToken.None);

        // Assert

        await act.Should().ThrowAsync<SqliteException>();
        context.Database.GetDbConnection().State.Should().Be(ConnectionState.Closed);
    }
}
