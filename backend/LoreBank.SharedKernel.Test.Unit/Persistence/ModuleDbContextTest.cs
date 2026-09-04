using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Persistence;

[TestFixture]
[TestOf(typeof(ModuleDbContext))]
public sealed class ModuleDbContextTest
{
    private SqliteConnection _connection = null!;

    [SetUp]
    public async Task OpenConnection()
    {
        // Sqlite en mémoire vit tant que la connexion est ouverte : on la garde
        // ouverte pour toute la durée du test, et les DbContext successifs la
        // partagent — ce qui permet à un second contexte d'observer ce que le
        // premier a réellement écrit.
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    [TearDown]
    public async Task CloseConnection()
    {
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task SaveChangesAsync_ShouldDispatchTheEventsOfTrackedEntities_WhenTheyHaveAny()
    {
        // Arrange

        var dispatcher = new RecordingDomainEventDispatcher();
        await using var context = CreateContext(dispatcher);
        await context.Database.EnsureCreatedAsync();

        var thing = new TestThing(Guid.NewGuid());
        thing.Happen();
        context.Things.Add(thing);

        // Act

        await context.SaveChangesAsync();

        // Assert

        dispatcher.Dispatched.Should().ContainSingle().Which.Should().BeOfType<SomethingHappened>();
    }

    [Test]
    public async Task SaveChangesAsync_ShouldClearTheEventsFromTheEntities_WhenItCollectsThem()
    {
        // Arrange

        var dispatcher = new RecordingDomainEventDispatcher();
        await using var context = CreateContext(dispatcher);
        await context.Database.EnsureCreatedAsync();

        var thing = new TestThing(Guid.NewGuid());
        thing.Happen();
        context.Things.Add(thing);

        // Act

        await context.SaveChangesAsync();

        // Assert

        thing.DomainEvents.Should().BeEmpty();
    }

    [Test]
    public async Task SaveChangesAsync_ShouldWriteBeforeDispatching_WhenAnEventIsDispatched()
    {
        // Arrange

        var rowsSeenAtDispatch = -1;
        var dispatcher = new RecordingDomainEventDispatcher();
        await using var context = CreateContext(dispatcher);
        await context.Database.EnsureCreatedAsync();

        dispatcher.OnDispatchAsync = async () => {
            // Un second DbContext sur la même connexion ne voit que ce qui est
            // réellement écrit, pas ce que le premier suit en mémoire.
            await using var probe = CreateContext(new RecordingDomainEventDispatcher());
            rowsSeenAtDispatch = await probe.Things.CountAsync();
        };

        var thing = new TestThing(Guid.NewGuid());
        thing.Happen();
        context.Things.Add(thing);

        // Act

        await context.SaveChangesAsync();

        // Assert

        rowsSeenAtDispatch.Should().Be(1);
    }

    [Test]
    public async Task SaveChangesAsync_ShouldSurfaceTheDispatcherException_WhenAHandlerFails()
    {
        // Arrange

        await using var context = CreateContext(new ThrowingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();

        var thing = new TestThing(Guid.NewGuid());
        thing.Happen();
        context.Things.Add(thing);

        // Act

        var act = async () => await context.SaveChangesAsync();

        // Assert

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Test]
    public async Task SaveChanges_ShouldThrow_WhenCalledSynchronously()
    {
        // Arrange

        // Les handlers sont async : les dispatcher depuis un SaveChanges sync
        // obligerait à bloquer. Plutôt qu'un dispatch perdu en silence ou un
        // sync-over-async, la famille synchrone est interdite.
        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();
        context.Things.Add(new TestThing(Guid.NewGuid()));

        // Act

        var act = () => context.SaveChanges();

        // Assert

        act.Should().Throw<NotSupportedException>().WithMessage("*SaveChangesAsync*");
    }

    [Test]
    public async Task OnModelCreating_ShouldIgnoreTheDomainEventsCollection_WhenAnEntityImplementsIHasDomainEvents()
    {
        // Arrange

        await using var context = CreateContext(new RecordingDomainEventDispatcher());

        // Act

        var entityType = context.Model.FindEntityType(typeof(TestThing))!;

        // Assert

        entityType
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .NotContain(nameof(TestThing.DomainEvents));
        entityType.GetNavigations().Should().BeEmpty();
    }

    private TestModuleDbContext CreateContext(IDomainEventDispatcher dispatcher) => new(
        options: new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite(_connection).Options,
        dispatcher: dispatcher
    );
}
