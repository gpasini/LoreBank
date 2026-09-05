using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// État statique comme les autres sondes du harnais : l'exécution en série est
// une hypothèse déclarée de l'assembly, la remise à zéro passe par ResetFakes.
public sealed class ProbeRecordingIntegrationEventHandler : IIntegrationEventHandler<ProbeIntegrationEvent>
{
    public static List<ProbeIntegrationEvent> Received { get; } = [];

    public static void Reset() => Received.Clear();

    public Task HandleAsync(
        ProbeIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        Received.Add(integrationEvent);

        return Task.CompletedTask;
    }
}
