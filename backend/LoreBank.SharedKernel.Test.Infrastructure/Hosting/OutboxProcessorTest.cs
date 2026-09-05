using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

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
            discriminant: "bank.probe-happened"
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
            discriminant: "bank.probe-happened"
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
            discriminant: "bank.probe-happened"
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
            discriminant: "bank.probe-happened"
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
            discriminant: "bank.probe-happened"
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
            discriminant: "bank.probe-happened"
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
            discriminant: "bank.probe-unhandled"
        ))!.Dispatched.Should().BeTrue();
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
