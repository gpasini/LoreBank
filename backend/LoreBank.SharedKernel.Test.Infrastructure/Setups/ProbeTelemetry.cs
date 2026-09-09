using System.Collections;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La sonde de Télémétrie du harnais (ADR 0025) : ce que l'hôte exporte,
// capté par l'exporteur mémoire d'OpenTelemetry — spans à leur fin,
// métriques à chaque collecte forcée. Singleton statique comme les autres
// sondes, remis à zéro par ResetFakes. Une source ou un meter oubliés dans
// la composition de l'hôte n'échouent nulle part : c'est ici qu'on le voit.
// Les collections sont synchronisées : un span serveur se termine après que
// le client a reçu sa réponse, donc parfois pendant le test suivant — un
// test isole les siens par le TraceId qu'il a lui-même envoyé.
public static class ProbeTelemetry
{
    public static SynchronizedList<Activity> Activities { get; } = [];

    public static SynchronizedList<MetricSnapshot> Metrics { get; } = [];

    public static void Attach(IServiceCollection services)
    {
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(Activities));
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddInMemoryExporter(Metrics));
    }

    // Les métriques ne sortent qu'à la collecte : le lecteur périodique du
    // SDK est trop lent pour un test, on force.
    public static void CollectMetrics(IServiceProvider services) =>
        services.GetRequiredService<MeterProvider>().ForceFlush();

    // Un traceparent W3C neuf, à envoyer en en-tête : le hosting le propage,
    // et tout ce que la requête produit porte ce TraceId.
    public static (string Header, ActivityTraceId TraceId) NewTraceParent()
    {
        var traceId = ActivityTraceId.CreateRandom();

        return ($"00-{traceId}-{ActivitySpanId.CreateRandom()}-01", traceId);
    }

    public static void Reset()
    {
        Activities.Clear();
        Metrics.Clear();
    }

    public sealed class SynchronizedList<T> : ICollection<T>
    {
        private readonly List<T> _items = [];

        private readonly Lock _lock = new();

        public int Count
        {
            get {
                lock (_lock) {
                    return _items.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            lock (_lock) {
                _items.Add(item);
            }
        }

        public void Clear()
        {
            lock (_lock) {
                _items.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (_lock) {
                return _items.Contains(item);
            }
        }

        public void CopyTo(
            T[] array,
            int arrayIndex
        )
        {
            lock (_lock) {
                _items.CopyTo(
                    array: array,
                    arrayIndex: arrayIndex
                );
            }
        }

        public bool Remove(T item)
        {
            lock (_lock) {
                return _items.Remove(item);
            }
        }

        // L'énumération porte sur un instantané : un span qui arrive pendant
        // l'assertion ne la fait pas trébucher.
        public IEnumerator<T> GetEnumerator()
        {
            lock (_lock) {
                return _items.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
