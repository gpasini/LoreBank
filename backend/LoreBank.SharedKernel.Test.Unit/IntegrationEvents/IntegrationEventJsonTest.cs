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
