using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

[IntegrationEvent("probe.ping")]
public sealed record PingIntegrationEvent(
    Guid ThingId,
    decimal Amount
) : IIntegrationEvent;
