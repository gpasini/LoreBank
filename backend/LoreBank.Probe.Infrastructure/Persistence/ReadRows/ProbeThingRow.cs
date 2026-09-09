namespace LoreBank.Probe.Infrastructure.Persistence.ReadRows;

// Le miroir plat de probe_things, réservé à la lecture (ADR 0018) : la row
// que la sonde de Liste requête (ListContractTest, ADR 0027).
public sealed record ProbeThingRow(
    Guid Id,
    string Label,
    string Kind,
    bool Active
);
