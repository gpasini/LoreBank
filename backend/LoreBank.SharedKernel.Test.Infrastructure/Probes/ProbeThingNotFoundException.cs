using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

public sealed class ProbeThingNotFoundException(Guid thingId) : NotFoundException(
    new() { ["thingId"] = thingId }
);
