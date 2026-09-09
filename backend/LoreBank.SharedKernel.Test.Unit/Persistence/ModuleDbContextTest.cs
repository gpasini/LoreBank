using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;
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

        dispatcher.Dispatched.Should().ContainSingle().Which.Should().BeOfType<SomethingHappenedDomainEvent>();
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

        dispatcher.OnDispatchAsync = async () =>
        {
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
        entityType
            .GetNavigations()
            .Select(navigation => navigation.Name)
            .Should()
            .NotContain(nameof(TestThing.DomainEvents));
    }

    [Test]
    public async Task OnModelCreating_ShouldApplyTheModuleSchema_WhenTheModelIsBuilt()
    {
        await using var context = CreateContext(new RecordingDomainEventDispatcher());

        context.Model
            .GetDefaultSchema()
            .Should()
            .Be(
                expected: "test",
                because: "HasDefaultSchema vit dans la base, pas dans une ligne à recopier par module"
            );
    }

    [Test]
    public async Task OnModelCreating_ShouldNotRequireConfigureModule_WhenAModuleHasNothingToConfigure()
    {
        await using var context = new BareModuleDbContext(
            options: new DbContextOptionsBuilder<BareModuleDbContext>().UseSqlite(_connection).Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );

        context.Model.GetDefaultSchema().Should().Be("bare");
    }

    // La Version d'agrégat (ADR 0020) : une convention du socle, pas une
    // ligne par module — TestThing ne la déclare nulle part.
    [Test]
    public async Task OnModelCreating_ShouldDeclareTheVersionAsAConcurrencyToken_WhenAnEntityIsAnAggregateRoot()
    {
        await using var context = CreateContext(new RecordingDomainEventDispatcher());

        var version = context.Model.FindEntityType(typeof(TestThing))!.FindProperty(ModuleDbContext.VersionPropertyName)!;

        version.IsShadowProperty().Should().BeTrue();
        version.IsConcurrencyToken.Should().BeTrue();
        version.GetColumnName().Should().Be(ModuleDbContext.VersionColumnName);
    }

    [Test]
    public async Task OnModelCreating_ShouldNotDeclareAVersion_WhenAnEntityIsNotAnAggregateRoot()
    {
        await using var context = new TestTransferDbContext(
            options: new DbContextOptionsBuilder<TestTransferDbContext>().UseSqlite(_connection).Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );

        context.Model.FindEntityType(typeof(TestTransfer))!.FindProperty(ModuleDbContext.VersionPropertyName)
            .Should().BeNull();
    }

    [Test]
    public async Task SaveChangesAsync_ShouldStartTheVersionAtZero_WhenAnAggregateIsInserted()
    {
        // Arrange

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();

        var thing = new TestThing(Guid.NewGuid());
        context.Things.Add(thing);

        // Act

        await context.SaveChangesAsync();

        // Assert

        (await VersionOfAsync(thing.Id)).Should().Be(0);
    }

    [Test]
    public async Task SaveChangesAsync_ShouldBumpTheVersion_WhenTheAggregateRootIsModified()
    {
        // Arrange

        var thing = await InsertThingAsync();

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        var loaded = await context.Things.SingleAsync(t => t.Id == thing.Id);
        loaded.Rename("renamed");

        // Act

        await context.SaveChangesAsync();

        // Assert

        (await VersionOfAsync(thing.Id)).Should().Be(1);
    }

    // Le piège : remplacer l'instance d'un VO owned laisse la racine Unchanged
    // aux yeux d'EF — la Version doit bouger quand même, sinon deux commandes
    // qui ne touchent que le solde se croiseraient sans être vues.
    [Test]
    public async Task SaveChangesAsync_ShouldBumpTheVersion_WhenOnlyAnOwnedValueObjectIsReplaced()
    {
        // Arrange

        var thing = await InsertThingAsync();

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        var loaded = await context.Things.SingleAsync(t => t.Id == thing.Id);
        loaded.Reprice(Money.Of(
            amount: 20m,
            currency: "EUR"
        ));

        // Act

        await context.SaveChangesAsync();

        // Assert

        (await VersionOfAsync(thing.Id)).Should().Be(1);
    }

    [Test]
    public async Task SaveChangesAsync_ShouldNotBumpTheVersion_WhenNothingChanged()
    {
        // Arrange

        var thing = await InsertThingAsync();

        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Things.SingleAsync(t => t.Id == thing.Id);

        // Act

        await context.SaveChangesAsync();

        // Assert

        (await VersionOfAsync(thing.Id)).Should().Be(0);
    }

    [Test]
    public async Task SaveChangesAsync_ShouldThrowConcurrentUpdateBeforeDispatching_WhenTheVersionIsStale()
    {
        // Arrange — deux contextes ont chargé la même version.

        var thing = await InsertThingAsync();

        await using var firstContext = CreateContext(new RecordingDomainEventDispatcher());
        var secondDispatcher = new RecordingDomainEventDispatcher();
        await using var secondContext = CreateContext(secondDispatcher);

        var firstView = await firstContext.Things.SingleAsync(t => t.Id == thing.Id);
        var secondView = await secondContext.Things.SingleAsync(t => t.Id == thing.Id);

        firstView.Rename("first");
        secondView.Rename("second");
        secondView.Happen();

        await firstContext.SaveChangesAsync();

        // Act

        var act = () => secondContext.SaveChangesAsync();

        // Assert — refusé, clé en primitive, aucun event parti.

        (await act.Should().ThrowAsync<ConcurrentUpdateException>())
            .Which.Parameters["id"].Should().Be(thing.Id);
        secondDispatcher.Dispatched.Should().BeEmpty();
    }

    private async Task<TestThing> InsertThingAsync()
    {
        await using var context = CreateContext(new RecordingDomainEventDispatcher());
        await context.Database.EnsureCreatedAsync();

        var thing = new TestThing(Guid.NewGuid());
        thing.Reprice(Money.Of(
            amount: 10m,
            currency: "EUR"
        ));
        context.Things.Add(thing);
        await context.SaveChangesAsync();

        return thing;
    }

    // Lue sur un contexte neuf : ce que la base porte, pas ce qu'un tracker
    // croit.
    private async Task<int> VersionOfAsync(Guid thingId)
    {
        await using var probe = CreateContext(new RecordingDomainEventDispatcher());
        var thing = await probe.Things.SingleAsync(t => t.Id == thingId);

        return probe.Entry(thing).Property<int>(ModuleDbContext.VersionPropertyName).CurrentValue;
    }

    private TestModuleDbContext CreateContext(IDomainEventDispatcher dispatcher) => new(
        options: new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite(_connection).Options,
        dispatcher: dispatcher
    );
}
