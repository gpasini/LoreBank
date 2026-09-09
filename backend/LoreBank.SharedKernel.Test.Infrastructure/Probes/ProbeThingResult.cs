namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

public sealed record ProbeThingResult(
    Guid Id,
    string Label,
    string Kind,
    bool Active
);
