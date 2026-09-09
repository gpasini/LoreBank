using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(IntegrationEventHandlers))]
public sealed class IntegrationEventHandlersTest
{
    [Test]
    public void DiscoverIn_ShouldRegisterEachClosedHandler_WithItsEventAndModule()
    {
        // Act

        var registrations = IntegrationEventHandlers.DiscoverIn(
            moduleName: "Probe",
            assemblies: [typeof(RecordingIntegrationEventHandler).Assembly]
        );

        // Assert

        var registration = registrations.Should()
            .ContainSingle(candidate => candidate.HandlerType == typeof(RecordingIntegrationEventHandler))
            .Subject;

        registration.EventType.Should().Be<PingIntegrationEvent>();
        registration.ModuleName.Should().Be("Probe");
        registration.Discriminant.Should().Be("probe.ping");
    }
}
