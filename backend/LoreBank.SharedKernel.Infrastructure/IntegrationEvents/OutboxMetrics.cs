using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// La mesure des outbox (ADR 0022) : deux jauges par module, en attente et
// poison, posées en System.Diagnostics.Metrics sans exporteur — ce
// qu'OpenTelemetry exportera le jour venu, ce que dotnet-counters lit dès
// aujourd'hui. Les valeurs sont celles du dernier rafraîchissement par le
// processor (à chaque passe), les jauges ne requêtent rien elles-mêmes.
public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "LoreBank.Outbox";

    public const string PendingGauge = "lorebank.outbox.pending";

    public const string PoisonedGauge = "lorebank.outbox.poisoned";

    public const string ModuleTag = "module";

    private readonly Meter _meter = new(MeterName);

    private readonly ConcurrentDictionary<string, OutboxDepth> _depths = new();

    public OutboxMetrics()
    {
        _meter.CreateObservableGauge(
            name: PendingGauge,
            observeValues: () => Measurements(depth => depth.Pending),
            unit: "{row}",
            description: "Lignes d'outbox en attente de livraison, par module."
        );
        _meter.CreateObservableGauge(
            name: PoisonedGauge,
            observeValues: () => Measurements(depth => depth.Poisoned),
            unit: "{row}",
            description: "Lignes d'outbox marquées poison, par module — à regarder par un humain."
        );
    }

    public void Record(
        string moduleName,
        OutboxDepth depth
    ) => _depths[moduleName] = depth;

    public void Dispose() => _meter.Dispose();

    private IEnumerable<Measurement<long>> Measurements(Func<OutboxDepth, long> pick) => _depths.Select(pair =>
        new Measurement<long>(
            value: pick(pair.Value),
            tags: new KeyValuePair<string, object?>(
                key: ModuleTag,
                value: pair.Key
            )
        )
    );
}

public readonly record struct OutboxDepth(
    long Pending,
    long Poisoned
);
