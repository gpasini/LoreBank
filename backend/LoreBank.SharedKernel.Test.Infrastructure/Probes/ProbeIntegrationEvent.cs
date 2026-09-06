using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La sonde des tests d'outbox : ancrée sur le ProbeModule (le discriminant
// désigne son schéma), comme les sondes de data migration s'ancrent sur
// ProbeDbContext — le socle se prouve sur son propre terrain, jamais sur un
// module d'exemple supprimable (ADR 0017).
[IntegrationEvent("probe.probe-happened")]
public sealed record ProbeIntegrationEvent(
    Guid ThingId,
    string Label
) : IIntegrationEvent;
