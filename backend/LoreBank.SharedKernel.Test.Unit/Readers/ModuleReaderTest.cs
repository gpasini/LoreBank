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

    // La row keyless lit la même table que le modèle d'écriture (ToView) : ce
    // qu'un agrégat écrit, sa row le relit — sans clé, sans tracking.
    [Test]
    public async Task Query_ShouldReadTheRow_WhenItExists()
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
    public async Task Query_ShouldReturnNull_WhenNoRowMatches()
    {
        // Act

        var row = await _reader.GetByIdAsync(
            id: Guid.NewGuid(),
            cancellationToken: CancellationToken.None
        );

        // Assert

        row.Should().BeNull();
    }

    // La règle « une lecture ne matérialise jamais d'agrégat » est mécanique :
    // un type à clé est refusé avant toute requête.
    [Test]
    public void Query_ShouldThrow_WhenTheTypeHasAKey()
    {
        var act = () => _reader.Expose<TestThing>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*jamais un agrégat*");
    }

    [Test]
    public void Query_ShouldThrow_WhenTheTypeIsNotInTheModel()
    {
        var act = () => _reader.Expose<UnmappedRow>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*n'est pas dans le modèle*");
    }

    private sealed record UnmappedRow(Guid Id);
}
