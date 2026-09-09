using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

[IntegrationEvent("fake.signalled")]
public sealed record SignallingIntegrationEvent(
    Guid ThingId,
    string Label
) : ISignalsClients
{
    public string ResourceKind => "fake-thing";

    public Guid ResourceId => ThingId;
}
