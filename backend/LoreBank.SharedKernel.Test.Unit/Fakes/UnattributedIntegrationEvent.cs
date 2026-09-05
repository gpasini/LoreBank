using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Volontairement sans [IntegrationEvent] : la sonde des chemins d'échec.
public sealed record UnattributedIntegrationEvent(Guid ThingId) : IIntegrationEvent;
