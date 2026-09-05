using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Publié sans aucun handler enregistré : la sonde du chemin « livré à
// personne ».
[IntegrationEvent("bank.probe-unhandled")]
public sealed record ProbeUnhandledIntegrationEvent(Guid ThingId) : IIntegrationEvent;
