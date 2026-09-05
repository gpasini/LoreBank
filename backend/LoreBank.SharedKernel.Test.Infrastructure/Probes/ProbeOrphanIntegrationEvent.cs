using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Discriminant qui ne désigne aucun module monté : la sonde de l'échec
// bruyant du publisher.
[IntegrationEvent("nowhere.probe-happened")]
public sealed record ProbeOrphanIntegrationEvent(Guid ThingId) : IIntegrationEvent;
