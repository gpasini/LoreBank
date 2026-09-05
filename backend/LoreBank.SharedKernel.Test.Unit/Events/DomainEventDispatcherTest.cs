using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Events;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.Events;

[TestFixture]
[TestOf(typeof(DomainEventDispatcher))]
public sealed class DomainEventDispatcherTest
{
    [Test]
    public async Task DispatchAsync_ShouldCallEveryHandlerOfTheEventType_WhenHandlersAreRegistered()
    {
        // Arrange

        var first = new RecordingDomainEventHandler();
        var second = new RecordingDomainEventHandler();
        var serviceProvider = new FakeServiceProvider();
        serviceProvider.Register<IDomainEventHandler<SomethingHappenedDomainEvent>>(
            first,
            second
        );

        // Act

        await new DomainEventDispatcher(serviceProvider).DispatchAsync(
            domainEvents: [new SomethingHappenedDomainEvent()],
            cancellationToken: CancellationToken.None
        );

        // Assert

        first.Calls.Should().Be(1);
        second.Calls.Should().Be(1);
    }

    [Test]
    public async Task DispatchAsync_ShouldIgnoreTheEvent_WhenNoHandlerIsRegistered()
    {
        // Arrange

        var serviceProvider = new FakeServiceProvider();

        // Act

        var act = async () => await new DomainEventDispatcher(serviceProvider).DispatchAsync(
            domainEvents: [new SomethingElseHappenedDomainEvent()],
            cancellationToken: CancellationToken.None
        );

        // Assert

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task DispatchAsync_ShouldSurfaceTheHandlerException_WhenAHandlerThrowsSynchronously()
    {
        // Arrange

        var serviceProvider = new FakeServiceProvider();
        serviceProvider.Register<IDomainEventHandler<SomethingHappenedDomainEvent>>(new ThrowingDomainEventHandler());

        // Act

        var act = async () => await new DomainEventDispatcher(serviceProvider).DispatchAsync(
            domainEvents: [new SomethingHappenedDomainEvent()],
            cancellationToken: CancellationToken.None
        );

        // Assert

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Test]
    public async Task DispatchAsync_ShouldSurfaceTheHandlerException_WhenAHandlerThrowsAfterAnAwait()
    {
        // Arrange

        // MethodInfo.Invoke n'emballe dans une TargetInvocationException que ce qui
        // est levé avant le premier await d'une méthode async : un throw après un
        // await fait échouer (fault) la Task retournée, et await la relève
        // directement, sans passer par le catch du dispatcher.
        var serviceProvider = new FakeServiceProvider();
        serviceProvider.Register<IDomainEventHandler<SomethingHappenedDomainEvent>>(new ThrowingAfterAwaitDomainEventHandler());

        // Act

        var act = async () => await new DomainEventDispatcher(serviceProvider).DispatchAsync(
            domainEvents: [new SomethingHappenedDomainEvent()],
            cancellationToken: CancellationToken.None
        );

        // Assert

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom after await");
    }

    [Test]
    public async Task DispatchAsync_ShouldRouteEachEventToItsOwnHandlers_WhenEventsAreOfMixedTypes()
    {
        // Arrange

        var handler = new RecordingDomainEventHandler();
        var serviceProvider = new FakeServiceProvider();
        serviceProvider.Register<IDomainEventHandler<SomethingHappenedDomainEvent>>(handler);

        // Act

        await new DomainEventDispatcher(serviceProvider).DispatchAsync(
            domainEvents: [new SomethingHappenedDomainEvent(), new SomethingElseHappenedDomainEvent(), new SomethingHappenedDomainEvent()],
            cancellationToken: CancellationToken.None
        );

        // Assert

        handler.Calls.Should().Be(2);
    }
}
