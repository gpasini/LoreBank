using System.Diagnostics.Metrics;

namespace LoreBank.SharedKernel.Infrastructure.Signals;

// La mesure des Signaux (ADR 0022, 0026) : une jauge des abonnés connectés
// et un compteur des Signaux livrés, en System.Diagnostics.Metrics sans
// exporteur — ce que l'hôte exporte par sa Télémétrie (ADR 0025), ce que
// dotnet-counters lit dès aujourd'hui. Pas de trace par Signal : le fait
// est déjà tracé par sa livraison d'outbox.
public sealed class SignalMetrics : IDisposable
{
    public const string MeterName = "LoreBank.Signals";

    public const string SubscribersGauge = "lorebank.signals.subscribers";

    public const string DeliveredCounter = "lorebank.signals.delivered";

    private readonly Meter _meter = new(MeterName);

    private readonly Counter<long> _delivered;

    private long _subscribers;

    public SignalMetrics()
    {
        _meter.CreateObservableGauge(
            name: SubscribersGauge,
            observeValue: () => Interlocked.Read(ref _subscribers),
            unit: "{subscriber}",
            description: "Clients abonnés au flux de Signaux, sur cette instance."
        );
        _delivered = _meter.CreateCounter<long>(
            name: DeliveredCounter,
            unit: "{signal}",
            description: "Signaux poussés aux abonnés, sur cette instance."
        );
    }

    public void SubscriberJoined() => Interlocked.Increment(ref _subscribers);

    public void SubscriberLeft() => Interlocked.Decrement(ref _subscribers);

    public void Delivered() => _delivered.Add(1);

    public void Dispose() => _meter.Dispose();
}
