using LoreBank.FakeModule.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Repositories;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Repositories;

[TestFixture]
[TestOf(typeof(ModuleRepository<,>))]
public sealed class ModuleRepositoryTest
{
    private SqliteConnection _connection = null!;

    [SetUp]
    public async Task OpenConnection()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    [TearDown]
    public async Task CloseConnection()
    {
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task GetRequiredByIdAsync_ShouldReturnTheAggregate_WhenItExists()
    {
        // Arrange

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();

        var thing = new TestThing(Guid.NewGuid());
        await new TestThingRepository(context).SaveAsync(
            aggregate: thing,
            cancellationToken: CancellationToken.None
        );

        // Act

        var loaded = await new TestThingRepository(context).GetRequiredByIdAsync(
            id: thing.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert

        loaded.Should().Be(thing);
    }

    [Test]
    public async Task GetRequiredByIdAsync_ShouldThrowTheModuleNotFound_WhenTheAggregateDoesNotExist()
    {
        // Arrange

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();

        // Act & Assert

        var act = () => new TestThingRepository(context).GetRequiredByIdAsync(
            id: Guid.NewGuid(),
            cancellationToken: CancellationToken.None
        );

        await act.Should().ThrowAsync<MissingThingException>();
    }

    [Test]
    public async Task SaveAsync_ShouldInsertTheAggregate_WhenItIsDetached()
    {
        // Arrange

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();

        // Act

        await new TestThingRepository(context).SaveAsync(
            aggregate: new TestThing(Guid.NewGuid()),
            cancellationToken: CancellationToken.None
        );

        // Assert

        await using var probe = CreateContext(new RecordingDomainEventDispatcher());
        (await probe.Things.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_ShouldNotDuplicateTheAggregate_WhenItIsAlreadyTracked()
    {
        // Arrange

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();

        var repository = new TestThingRepository(context);
        var thing = new TestThing(Guid.NewGuid());
        await repository.SaveAsync(
            aggregate: thing,
            cancellationToken: CancellationToken.None
        );

        // Act

        await repository.SaveAsync(
            aggregate: thing,
            cancellationToken: CancellationToken.None
        );

        // Assert

        await using var probe = CreateContext(new RecordingDomainEventDispatcher());
        (await probe.Things.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_ShouldDispatchTheDomainEvents_WhenTheAggregateHasSome()
    {
        // Arrange

        // SaveAsync passe par le SaveChangesAsync de ModuleDbContext : c'est
        // ce qui déclenche le dispatch (ADR 0003) — l'invariant que ce test
        // épingle, pour qu'un futur repository maison ne le perde pas.
        var dispatcher = new RecordingDomainEventDispatcher();
        await using var context = CreateContext(dispatcher);
        await context.Database.EnsureCreatedAsync();

        var thing = new TestThing(Guid.NewGuid());
        thing.Happen();

        // Act

        await new TestThingRepository(context).SaveAsync(
            aggregate: thing,
            cancellationToken: CancellationToken.None
        );

        // Assert

        dispatcher.Dispatched.Should().ContainSingle().Which.Should().BeOfType<SomethingHappenedDomainEvent>();
    }

    private TestModuleDbContext CreateContext(IDomainEventDispatcher dispatcher) => new(
        options: new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite(_connection).Options,
        dispatcher: dispatcher
    );
}
