using System.Diagnostics.Metrics;
using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Domain.ValueObjects;
using LoreBank.SharedKernel.Infrastructure.Signals;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoreBank.SharedKernel.Test.Unit.Signals;

// Le fan-out en mémoire : chaque test ouvre un abonnement, publie, et lit
// — l'abonnement n'existe qu'une fois l'énumération commencée, d'où le
// MoveNextAsync lancé avant la publication.
[TestFixture]
[TestOf(typeof(SignalHub))]
public sealed class SignalHubTest
{
    private readonly static Guid AccountId = Guid.NewGuid();

    private readonly static Signal Deposited = new(
        Discriminant: "bank.money-deposited",
        ResourceKind: "bank-account",
        ResourceId: AccountId,
        OccurredAt: DateTimeOffset.UnixEpoch
    );

    private SignalMetrics _metrics = null!;

    [SetUp]
    public void SetUp() => _metrics = new SignalMetrics();

    [TearDown]
    public void TearDown() => _metrics.Dispose();

    [Test]
    public async Task Publish_ShouldDeliverTheSignal_ToASubscriberWhoseFilterMatches()
    {
        // Arrange

        var hub = Hub(RulingSignalPolicy.AllowAll);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var subscription = hub.SubscribeAsync(
            filter: SignalFilter.Parse([$"bank-account/{AccountId}"]),
            actor: Actor.Anonymous,
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);

        var next = subscription.MoveNextAsync();

        // Act

        hub.Publish(Deposited);

        // Assert

        (await next).Should().BeTrue();
        subscription.Current.Should().Be(Deposited);
    }

    [Test]
    public async Task Publish_ShouldSkipASubscriber_WhoseFilterDoesNotMatch()
    {
        // Arrange

        var hub = Hub(RulingSignalPolicy.AllowAll);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var subscription = hub.SubscribeAsync(
            filter: SignalFilter.Parse([$"bank-account/{AccountId}"]),
            actor: Actor.Anonymous,
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);

        var next = subscription.MoveNextAsync();

        // Act — un autre compte d'abord, puis le bon.

        hub.Publish(Deposited with { ResourceId = Guid.NewGuid() });
        hub.Publish(Deposited);

        // Assert — le premier Signal lu est celui du filtre.

        (await next).Should().BeTrue();
        subscription.Current.Should().Be(Deposited);
    }

    [Test]
    public async Task Publish_ShouldSkipASubscriber_WhenThePolicyRefusesIt()
    {
        // Arrange — la policy ne laisse passer que l'Acteur « alice ».

        var hub = Hub(new RulingSignalPolicy((
            actor,
            _
        ) => !actor.IsAnonymous && actor.Id == "alice"));

        using var cancellation = new CancellationTokenSource();

        var anonymous = hub.SubscribeAsync(
            filter: SignalFilter.All,
            actor: Actor.Anonymous,
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);
        var alice = hub.SubscribeAsync(
            filter: SignalFilter.All,
            actor: Actor.Of("alice"),
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);

        var anonymousNext = anonymous.MoveNextAsync().AsTask();
        var aliceNext = alice.MoveNextAsync().AsTask();

        // Act

        hub.Publish(Deposited);

        // Assert

        (await aliceNext.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        alice.Current.Should().Be(Deposited);
        anonymousNext.IsCompleted.Should().BeFalse();

        // Un itérateur ne se dispose pas pendant qu'un MoveNextAsync est en
        // vol : on annule, on attend, puis on rend.
        await cancellation.CancelAsync();
        await Settle(anonymousNext);
        await anonymous.DisposeAsync();
        await alice.DisposeAsync();
    }

    [Test]
    public async Task Publish_ShouldCloseTheSubscription_WhenTheSubscriberOverflowsItsChannel()
    {
        // Arrange — un abonné qui ne lit pas.

        var hub = Hub(RulingSignalPolicy.AllowAll);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var subscription = hub.SubscribeAsync(
            filter: SignalFilter.All,
            actor: Actor.Anonymous,
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);

        var first = subscription.MoveNextAsync();

        // Act — deux fois la capacité : le canal déborde quoi qu'ait pris la
        // lecture en attente.

        for (var i = 0; i < 2 * SignalHub.SubscriberCapacity; i++) {
            hub.Publish(Deposited with { ResourceId = Guid.NewGuid() });
        }

        // Assert — ce qui tenait dans le canal se lit, puis le flux se
        // termine : aucun Signal perdu en silence, le client reconnectera.

        var received = 0;

        for (var moved = await first; moved; moved = await subscription.MoveNextAsync()) {
            received++;
        }

        received.Should().BeInRange(
            minimumValue: SignalHub.SubscriberCapacity,
            maximumValue: SignalHub.SubscriberCapacity + 1
        );
        hub.SubscriberCount.Should().Be(0);
    }

    [Test]
    public async Task SubscribeAsync_ShouldCountTheSubscriber_WhileItEnumerates()
    {
        // Arrange

        var hub = Hub(RulingSignalPolicy.AllowAll);

        using var cancellation = new CancellationTokenSource();

        var subscription = hub.SubscribeAsync(
            filter: SignalFilter.All,
            actor: Actor.Anonymous,
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);

        hub.SubscriberCount.Should().Be(0);

        // Act

        var next = subscription.MoveNextAsync();

        // Assert

        hub.SubscriberCount.Should().Be(1);
        Gauge().Should().Be(1);

        // Act — le client part.

        await cancellation.CancelAsync();

        var act = async () => await next;

        await act.Should().ThrowAsync<OperationCanceledException>();
        await subscription.DisposeAsync();

        // Assert

        hub.SubscriberCount.Should().Be(0);
        Gauge().Should().Be(0);
    }

    [Test]
    public async Task Publish_ShouldCountEachDelivery_OnTheSocleMeter()
    {
        // Arrange

        var hub = Hub(RulingSignalPolicy.AllowAll);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var subscription = hub.SubscribeAsync(
            filter: SignalFilter.All,
            actor: Actor.Anonymous,
            cancellationToken: cancellation.Token
        ).GetAsyncEnumerator(cancellation.Token);

        var next = subscription.MoveNextAsync();

        var delivered = 0L;

        using var listener = new MeterListener();

        listener.InstrumentPublished = (
            instrument,
            meterListener
        ) =>
        {
            if (instrument.Meter == Meter(_metrics) && instrument.Name == SignalMetrics.DeliveredCounter) {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((
                _,
                measurement,
                _,
                _
            ) => delivered += measurement
        );
        listener.Start();

        // Act

        hub.Publish(Deposited);
        hub.Publish(Deposited);

        await next;

        // Assert

        delivered.Should().Be(2);
    }

    private static async Task Settle(Task<bool> next)
    {
        try {
            await next;
        } catch (OperationCanceledException) {
        }
    }

    private SignalHub Hub(ISignalPolicy policy) => new(
        policy: policy,
        metrics: _metrics,
        logger: NullLogger<SignalHub>.Instance
    );

    // La jauge des abonnés du Meter de ce test, lue comme un exporteur le
    // ferait — plusieurs Meters « LoreBank.Signals » peuvent vivre dans le
    // process (un par test), on ne lit que celui de la fixture.
    private long Gauge()
    {
        long? value = null;

        using var listener = new MeterListener();

        listener.InstrumentPublished = (
            instrument,
            meterListener
        ) =>
        {
            if (instrument.Meter == Meter(_metrics) && instrument.Name == SignalMetrics.SubscribersGauge) {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((
                _,
                measurement,
                _,
                _
            ) => value = measurement
        );
        listener.Start();
        listener.RecordObservableInstruments();

        return value ?? throw new InvalidOperationException("La jauge des abonnés n'a pas été observée.");
    }

    private static Meter Meter(SignalMetrics metrics) =>
        (Meter) typeof(SignalMetrics)
            .GetField(
                name: "_meter",
                bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            )!
            .GetValue(metrics)!;
}
