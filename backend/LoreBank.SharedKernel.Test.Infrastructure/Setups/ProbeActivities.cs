using System.Diagnostics;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La sonde des traces d'outbox du socle : un ActivityListener qui écoute la
// source « LoreBank.Outbox » comme un exporteur le ferait — sans en être un —
// et garde chaque activité terminée. Le socle se prouve en BCL, sans
// OpenTelemetry (ADR 0025) ; c'est le harnais de l'hôte qui prouve l'export.
public sealed class ProbeActivities : IDisposable
{
    private readonly ActivityListener _listener;

    public List<Activity> Stopped { get; } = [];

    private ProbeActivities()
    {
        _listener = new ActivityListener {
            ShouldListenTo = source => source.Name == OutboxTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Stopped.Add(activity),
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public static ProbeActivities Listen() => new();

    public void Dispose() => _listener.Dispose();
}
