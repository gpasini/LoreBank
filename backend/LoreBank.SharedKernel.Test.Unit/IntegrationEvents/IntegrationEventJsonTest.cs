using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(IntegrationEventJson))]
public sealed class IntegrationEventJsonTest
{
    [Test]
    public void Serialize_ShouldWriteCamelCaseKeys()
    {
        // Les clés partent telles quelles chez les consommateurs : la casse
        // est un contrat, comme celle des paramètres de ProblemDetails.
        var payload = IntegrationEventJson.Serialize(new PingIntegrationEvent(
            ThingId: Guid.Empty,
            Amount: 12.5m
        ));

        payload.Should().Contain("\"thingId\"");
        payload.Should().Contain("\"amount\"");
    }

    [Test]
    public void Serialize_ShouldLeaveTheSignalResourceOut_WhenTheEventSignalsClients()
    {
        // La ressource vit dans ses colonnes (ADR 0026) : le payload reste le
        // langage publié de l'event, rien de plus — et se relit tel quel.
        var original = new SignallingIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Label: "signalé"
        );

        var payload = IntegrationEventJson.Serialize(original);

        payload.Should().NotContain("resourceKind").And.NotContain("resourceId");
        payload.Should().Contain("\"thingId\"").And.Contain("\"label\"");
        IntegrationEventJson.Deserialize(
            payload: payload,
            eventType: typeof(SignallingIntegrationEvent)
        ).Should().Be(original);
    }

    [Test]
    public void Deserialize_ShouldRoundTripTheEvent()
    {
        // Arrange

        var original = new PingIntegrationEvent(
            ThingId: Guid.NewGuid(),
            Amount: 42.10m
        );

        // Act

        var deserialized = IntegrationEventJson.Deserialize(
            payload: IntegrationEventJson.Serialize(original),
            eventType: typeof(PingIntegrationEvent)
        );

        // Assert

        deserialized.Should().Be(original);
    }
}
