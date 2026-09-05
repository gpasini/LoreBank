using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(IntegrationEventHandlerRegistration))]
public sealed class IntegrationEventHandlerRegistrationTest
{
    [Test]
    public void Constructor_ShouldThrow_WhenTheHandlerDoesNotHandleTheEvent()
    {
        var act = () => new IntegrationEventHandlerRegistration(
            handlerType: typeof(string),
            eventType: typeof(PingIntegrationEvent),
            moduleName: "Probe"
        );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*IIntegrationEventHandler<PingIntegrationEvent>*");
    }

    [Test]
    public void Constructor_ShouldThrow_WhenTheEventHasNoDiscriminant()
    {
        // Une registration mal formée doit casser à la composition de l'hôte,
        // pas au premier event livré.
        var act = () => new IntegrationEventHandlerRegistration(
            handlerType: typeof(RecordingIntegrationEventHandler),
            eventType: typeof(UnattributedIntegrationEvent),
            moduleName: "Probe"
        );

        act.Should().Throw<InvalidOperationException>();
    }
}
