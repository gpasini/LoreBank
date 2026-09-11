using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.ValueObjects;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Infrastructure.Signals;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le suiveur sur le terrain probe (ADR 0026) : la ligne marquée livrée est
// la notification. Les passes sont pilotées à la main — livraison puis
// suivi — et le hub de l'hôte est observé par un abonnement direct. Le
// premier passage du suiveur amorce son curseur : chaque test l'amorce
// avant de publier.
[TestFixture]
[TestOf(typeof(SignalTailer))]
public sealed class SignalTailerTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private readonly static TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [SetUp]
    public async Task SetUp()
    {
        await ProbeOutbox.CleanAsync(Factory);
        await SignalProbe.TailAsync(Factory);
    }

    [TearDown]
    public async Task TearDown() => await ProbeOutbox.CleanAsync(Factory);

    [Test]
    public async Task TailAsync_ShouldSignalTheRow_OnceItIsDispatched()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        var thingId = Guid.NewGuid();

        await using var subscription = Subscribe();

        var next = subscription.MoveNextAsync().AsTask();

        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: thingId,
                Label: "signalée"
            )
        );

        // Act — en attente : rien. Livrée : signalée.

        await SignalProbe.TailAsync(Factory);

        next.IsCompleted.Should().BeFalse();

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);

        // Assert

        (await next.WaitAsync(Timeout)).Should().BeTrue();

        var signal = subscription.Current;

        signal.Discriminant.Should().Be("probe.probe-happened");
        signal.ResourceKind.Should().Be(ProbeIntegrationEvent.ThingResourceKind);
        signal.ResourceId.Should().Be(thingId);
        signal.OccurredAt.Should().BeCloseTo(
            nearbyTime: DateTimeOffset.UtcNow,
            precision: TimeSpan.FromMinutes(1)
        );

        await Settle(subscription.MoveNextAsync().AsTask());
    }

    [Test]
    public async Task TailAsync_ShouldNotSignalTheRow_WhileAHandlerFails()
    {
        // Arrange — la sonde échoueuse échoue : la ligne reste en attente.

        await using var subscription = Subscribe();

        var next = subscription.MoveNextAsync().AsTask();

        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: Guid.NewGuid(),
                Label: "retardée"
            )
        );

        // Act

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);

        // Assert — après livraison seulement : toutes les conséquences du
        // fait sont en base quand le client l'apprend.

        next.IsCompleted.Should().BeFalse();

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);

        (await next.WaitAsync(Timeout)).Should().BeTrue();

        await Settle(subscription.MoveNextAsync().AsTask());
    }

    [Test]
    public async Task TailAsync_ShouldSignalEachRowOnce_AcrossPasses()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        await using var subscription = Subscribe();

        var first = subscription.MoveNextAsync().AsTask();

        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: Guid.NewGuid(),
                Label: "une fois"
            )
        );

        await ProcessPendingAsync();

        // Act — trois passes du suiveur sur la même ligne livrée.

        await SignalProbe.TailAsync(Factory);
        await SignalProbe.TailAsync(Factory);
        await SignalProbe.TailAsync(Factory);

        // Assert — la fenêtre de recouvrement relit la ligne, la mémoire des
        // ids ne la signale qu'une fois.

        (await first.WaitAsync(Timeout)).Should().BeTrue();

        var second = subscription.MoveNextAsync().AsTask();

        await Task.Delay(200);

        second.IsCompleted.Should().BeFalse();

        await Settle(second);
    }

    [Test]
    public async Task TailAsync_ShouldSkipTheRow_WhenTheEventDoesNotSignalClients()
    {
        // Arrange — un event publié sans marqueur : livré, jamais signalé.

        await using var subscription = Subscribe();

        var next = subscription.MoveNextAsync().AsTask();

        await PublishAsync(new ProbeUnhandledIntegrationEvent(Guid.NewGuid()));

        // Act

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);

        // Assert

        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-unhandled"
        ))!.Dispatched.Should().BeTrue();

        await Task.Delay(200);

        next.IsCompleted.Should().BeFalse();

        await Settle(next);
    }

    [Test]
    public async Task TailAsync_ShouldSignalOnEveryInstance_WhenEachFollowsItsOwnCursor()
    {
        // Arrange — une seconde instance de l'hôte : son propre hub, son
        // propre suiveur, la même base. Amorcée comme la première.

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        using var otherMetrics = new SignalMetrics();

        var otherHub = new SignalHub(
            policy: new AllowAllSignalPolicy(),
            metrics: otherMetrics,
            logger: NullLogger<SignalHub>.Instance
        );
        var otherTailer = new SignalTailer(
            modules: Factory.Services.GetRequiredService<IEnumerable<IHostModule>>(),
            stores: Factory.Services.GetRequiredService<IntegrationEventStores>(),
            hub: otherHub
        );

        await otherTailer.TailAsync(CancellationToken.None);

        using var cancellation = new CancellationTokenSource();

        await using var here = Subscribe(cancellation.Token);
        await using var there = otherHub.SubscribeAsync(
            filter: SignalFilter.All,
            actor: Actor.Anonymous,
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);

        var hereNext = here.MoveNextAsync().AsTask();
        var thereNext = there.MoveNextAsync().AsTask();

        var thingId = Guid.NewGuid();

        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: thingId,
                Label: "partout"
            )
        );

        // Act — livrée par une passe, suivie par les deux instances.

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);
        await otherTailer.TailAsync(CancellationToken.None);

        // Assert

        (await hereNext.WaitAsync(Timeout)).Should().BeTrue();
        (await thereNext.WaitAsync(Timeout)).Should().BeTrue();
        here.Current.ResourceId.Should().Be(thingId);
        there.Current.ResourceId.Should().Be(thingId);
    }

    private static IAsyncEnumerator<Signal> Subscribe(CancellationToken cancellationToken = default) =>
        Factory.Services
            .GetRequiredService<SignalHub>()
            .SubscribeAsync(
                filter: SignalFilter.All,
                actor: Actor.Anonymous,
                cancellationToken: cancellationToken
            )
            .GetAsyncEnumerator(cancellationToken);

    // Un itérateur ne se dispose pas pendant qu'un MoveNextAsync est en vol :
    // un Signal de plus, poussé à la main, termine la lecture en attente.
    private static async Task Settle(Task<bool> pending)
    {
        Factory.Services.GetRequiredService<SignalHub>().Publish(new Signal(
                Discriminant: "probe.settle",
                ResourceKind: "probe-thing",
                ResourceId: Guid.Empty,
                OccurredAt: DateTimeOffset.UtcNow
            )
        );

        await pending.WaitAsync(Timeout);
    }

    private static Task ProcessPendingAsync() =>
        Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

    private static async Task PublishAsync(IIntegrationEvent integrationEvent)
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<IIntegrationEventPublisher>()
            .PublishAsync(
                integrationEvent: integrationEvent,
                cancellationToken: CancellationToken.None
            );
    }
}
