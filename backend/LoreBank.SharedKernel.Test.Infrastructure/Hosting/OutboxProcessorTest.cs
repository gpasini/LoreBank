using System.Diagnostics.Metrics;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Les passes sont pilotées à la main (la cadence de fond est neutralisée par
// le harnais) : chaque test publie pour de vrai, déclenche une passe, et
// observe l'outbox, l'inbox et les compteurs des sondes. Le harnais resserre
// MaxAttempts à 2 et met le backoff à 0 — les tests pilotent les passes, pas
// l'horloge.
[TestFixture]
[TestOf(typeof(OutboxProcessor))]
public sealed class OutboxProcessorTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [SetUp]
    public async Task SetUp() => await ProbeOutbox.CleanAsync(Factory);

    [TearDown]
    public async Task TearDown() => await ProbeOutbox.CleanAsync(Factory);

    [Test]
    public async Task ProcessPendingAsync_ShouldDeliverToEachHandler_AndMarkTheRowDispatched()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        var thingId = Guid.NewGuid();

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: thingId,
            Label: "livraison"
        ));

        // Act

        await ProcessPendingAsync();

        // Assert

        var received = ProbeRecordingIntegrationEventHandler.Received
            .Should().ContainSingle().Subject;

        received.ThingId.Should().Be(thingId);
        received.Label.Should().Be("livraison");

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        row!.Dispatched.Should().BeTrue();

        // Une ligne d'inbox par handler : c'est elle qui rend le rejeu inoffensif.
        (await ProbeOutbox.CountInboxAsync(
            factory: Factory,
            eventId: row.Id
        )).Should().Be(2);
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldSkipHandledEvents_WhenARowIsRedelivered()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "rejeu"
        ));

        await ProcessPendingAsync();

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        // Un crash entre le commit des handlers et le marquage de l'outbox
        // rejoue l'event : c'est le scénario at-least-once par construction.
        await ProbeOutbox.RedeliverAsync(
            factory: Factory,
            id: row!.Id
        );

        // Act

        await ProcessPendingAsync();

        // Assert

        // L'inbox a rendu le rejeu inoffensif : aucun handler réinvoqué, et la
        // ligne re-marquée livrée.
        ProbeRecordingIntegrationEventHandler.Received.Should().ContainSingle();
        ProbeFailingIntegrationEventHandler.Invocations.Should().Be(1);

        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        ))!.Dispatched.Should().BeTrue();
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldRetryOnlyTheFailedHandler_EachInItsOwnTransaction()
    {
        // Arrange

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "échec partiel"
        ));

        // Act — première passe : la sonde enregistreuse réussit, l'autre échoue.

        await ProcessPendingAsync();

        // Assert

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        row!.Dispatched.Should().BeFalse();
        row.Attempts.Should().Be(1);
        row.LastError.Should().Contain("Échec volontaire");

        // La transaction du handler échoué s'est annulée seule : la ligne
        // d'inbox du handler réussi a, elle, bien commité.
        (await ProbeOutbox.CountInboxAsync(
            factory: Factory,
            eventId: row.Id
        )).Should().Be(1);

        // Act — seconde passe : l'échec est levé, seule la sonde échouée rejoue.

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        await ProcessPendingAsync();

        // Assert

        ProbeRecordingIntegrationEventHandler.Received.Should().ContainSingle();
        ProbeFailingIntegrationEventHandler.Invocations.Should().Be(2);

        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        ))!.Dispatched.Should().BeTrue();
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldPoisonTheRow_AfterMaxAttempts()
    {
        // Arrange

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "poison"
        ));

        // Act — MaxAttempts vaut 2 dans le harnais : deux passes échouées.

        await ProcessPendingAsync();
        await ProcessPendingAsync();

        // Assert

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        row!.Poisoned.Should().BeTrue();
        row.Dispatched.Should().BeFalse();
        row.Attempts.Should().Be(2);
        row.LastError.Should().Contain("Échec volontaire");

        // Act — une passe de plus : la ligne poison est sortie de la file.

        await ProcessPendingAsync();

        // Assert

        ProbeFailingIntegrationEventHandler.Invocations.Should().Be(2);
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldMarkTheRowDispatched_WhenNoHandlerIsRegistered()
    {
        // Arrange

        await PublishAsync(new ProbeUnhandledIntegrationEvent(Guid.NewGuid()));

        // Act

        await ProcessPendingAsync();

        // Assert

        // Livré à personne est livré quand même : un event sans consommateur
        // ne doit pas rester pending pour toujours.
        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-unhandled"
        ))!.Dispatched.Should().BeTrue();
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldReserveTheBatch_WhenAnotherPassRunsConcurrently()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;
        ProbeRecordingIntegrationEventHandler.Gate = new TaskCompletionSource();

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "réservation"
        ));

        // Act — la première passe réserve le lot et s'arrête dans le handler ;
        // la seconde, lancée à côté comme le ferait une autre instance de
        // l'hôte, doit rendre la main sans rien livrer.

        var firstPass = ProcessPendingAsync();

        await ProbeRecordingIntegrationEventHandler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var secondPass = ProcessPendingAsync();

        await secondPass.WaitAsync(TimeSpan.FromSeconds(5));

        ProbeRecordingIntegrationEventHandler.Gate.SetResult();

        await firstPass;

        // Assert

        ProbeRecordingIntegrationEventHandler.Received.Should().ContainSingle();
        ProbeFailingIntegrationEventHandler.Invocations.Should().Be(1);

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        row!.Dispatched.Should().BeTrue();
        row.Attempts.Should().Be(0);
        row.Reserved.Should().BeFalse();

        (await ProbeOutbox.CountInboxAsync(
            factory: Factory,
            eventId: row.Id
        )).Should().Be(2);
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldSkipAReservedRow_UntilItsReservationExpires()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "reprise"
        ));

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        // Act — une autre instance tient la ligne : la passe la laisse.

        await ProbeOutbox.ReserveAsync(
            factory: Factory,
            id: row!.Id,
            fromNow: TimeSpan.FromHours(1)
        );

        await ProcessPendingAsync();

        // Assert

        ProbeRecordingIntegrationEventHandler.Received.Should().BeEmpty();

        // Act — l'instance a disparu, son bail est échu : la passe reprend la ligne.

        await ProbeOutbox.ReserveAsync(
            factory: Factory,
            id: row.Id,
            fromNow: TimeSpan.FromHours(-1)
        );

        await ProcessPendingAsync();

        // Assert

        ProbeRecordingIntegrationEventHandler.Received.Should().ContainSingle();

        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        ))!.Dispatched.Should().BeTrue();
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldReleaseTheReservation_WhenAHandlerFails()
    {
        // Arrange

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "échec"
        ));

        // Act

        await ProcessPendingAsync();

        // Assert — le backoff seul décide de la prochaine tentative, pas le bail.

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        row!.Attempts.Should().Be(1);
        row.Dispatched.Should().BeFalse();
        row.Reserved.Should().BeFalse();
    }

    [Test]
    public async Task PurgeExpiredAsync_ShouldDeleteADispatchedRowAndItsInbox_WhenOlderThanRetention()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "purge"
        ));

        await ProcessPendingAsync();

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        await ProbeOutbox.BackdateAsync(
            factory: Factory,
            id: row!.Id,
            by: BeyondRetention
        );

        // Act

        await PurgeExpiredAsync();

        // Assert

        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        )).Should().BeNull();

        (await ProbeOutbox.CountInboxAsync(
            factory: Factory,
            eventId: row.Id
        )).Should().Be(0);
    }

    [Test]
    public async Task PurgeExpiredAsync_ShouldKeepADispatchedRow_WhenWithinRetention()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "récente"
        ));

        await ProcessPendingAsync();

        // Act

        await PurgeExpiredAsync();

        // Assert

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        row.Should().NotBeNull();

        (await ProbeOutbox.CountInboxAsync(
            factory: Factory,
            eventId: row!.Id
        )).Should().Be(2);
    }

    [Test]
    public async Task PurgeExpiredAsync_ShouldKeepAPoisonedRow_WhenOlderThanRetention()
    {
        // Arrange — MaxAttempts vaut 2 dans le harnais : deux passes échouées.

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "poison ancien"
        ));

        await ProcessPendingAsync();
        await ProcessPendingAsync();

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        row!.Poisoned.Should().BeTrue();

        await ProbeOutbox.BackdateAsync(
            factory: Factory,
            id: row.Id,
            by: BeyondRetention
        );

        // Act

        await PurgeExpiredAsync();

        // Assert — une ligne poison reste pour un humain, quel que soit son âge.

        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        ))!.Poisoned.Should().BeTrue();
    }

    [Test]
    public async Task PurgeExpiredAsync_ShouldKeepAPendingRow_WhenOlderThanRetention()
    {
        // Arrange

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "en attente"
        ));

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        );

        await ProbeOutbox.BackdateAsync(
            factory: Factory,
            id: row!.Id,
            by: BeyondRetention
        );

        // Act

        await PurgeExpiredAsync();

        // Assert

        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "probe.probe-happened"
        ))!.Dispatched.Should().BeFalse();
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldMeasureTheOutbox_AfterEachPass()
    {
        // Arrange — une ligne dont un handler échoue : en attente après la
        // première passe, poison après la seconde (MaxAttempts vaut 2).

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "mesure"
        ));

        // Act & Assert

        await ProcessPendingAsync();

        Gauges().Should().BeEquivalentTo(new Dictionary<string, long> {
            [OutboxMetrics.PendingGauge] = 1,
            [OutboxMetrics.PoisonedGauge] = 0,
        });

        await ProcessPendingAsync();

        Gauges().Should().BeEquivalentTo(new Dictionary<string, long> {
            [OutboxMetrics.PendingGauge] = 0,
            [OutboxMetrics.PoisonedGauge] = 1,
        });
    }

    [Test]
    public async Task PurgeExpiredAsync_ShouldWarnPerModule_WhenPoisonedRowsRemain()
    {
        // Arrange

        await PublishAsync(new ProbeIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "poison signalé"
        ));

        await ProcessPendingAsync();
        await ProcessPendingAsync();

        ProbeLogs.Reset();

        // Act

        await PurgeExpiredAsync();

        // Assert

        var warning = ProbeLogs.Entries
            .Should().ContainSingle(entry => entry.Level == LogLevel.Warning)
            .Subject;

        warning.Message.Should().Contain("Probe").And.Contain("1");
    }

    [Test]
    public async Task PurgeExpiredAsync_ShouldStaySilent_WhenNoRowIsPoisoned()
    {
        // Arrange

        ProbeLogs.Reset();

        // Act

        await PurgeExpiredAsync();

        // Assert

        ProbeLogs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Warning);
    }

    // Les jauges du Meter du socle pour le module Probe, lues comme un
    // exporteur le ferait : un MeterListener observe les instruments, la
    // valeur est celle du dernier rafraîchissement.
    private static Dictionary<string, long> Gauges()
    {
        var values = new Dictionary<string, long>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (
            instrument,
            meterListener
        ) => {
            if (instrument.Meter.Name == OutboxMetrics.MeterName) {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((
                instrument,
                measurement,
                tags,
                _
            ) => {
                foreach (var tag in tags) {
                    if (tag.Key == OutboxMetrics.ModuleTag && Equals(
                            objA: tag.Value,
                            objB: "Probe"
                        )) {
                        values[instrument.Name] = measurement;
                    }
                }
            }
        );

        listener.Start();
        listener.RecordObservableInstruments();

        return values;
    }

    // La rétention par défaut est de 7 jours ; le harnais ne la resserre
    // pas, les tests antidatent au-delà.
    private static readonly TimeSpan BeyondRetention = TimeSpan.FromDays(8);

    private static Task PurgeExpiredAsync() =>
        Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .PurgeExpiredAsync(CancellationToken.None);

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
