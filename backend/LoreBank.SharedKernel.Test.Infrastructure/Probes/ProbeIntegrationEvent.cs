using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La sonde des tests d'outbox : ancrée sur le module Bank (le discriminant
// désigne son schéma), comme les sondes de data migration s'ancrent sur
// BankDbContext — le module de référence sert de terrain, son code n'est
// jamais touché.
[IntegrationEvent("bank.probe-happened")]
public sealed record ProbeIntegrationEvent(
    Guid ThingId,
    string Label
) : IIntegrationEvent;
