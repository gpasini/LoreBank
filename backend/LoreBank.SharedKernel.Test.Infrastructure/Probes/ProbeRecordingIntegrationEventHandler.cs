using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// État statique comme les autres sondes du harnais : l'exécution en série est
// une hypothèse déclarée de l'assembly, la remise à zéro passe par ResetFakes.
// La barrière sert au test de concurrence du processor : posée, elle retient
// le handler à l'intérieur de sa transaction — le temps qu'une seconde passe
// tourne à côté — et Entered signale qu'il y est.
public sealed class ProbeRecordingIntegrationEventHandler : IIntegrationEventHandler<ProbeIntegrationEvent>
{
    public static List<ProbeIntegrationEvent> Received { get; } = [];

    public static TaskCompletionSource? Gate { get; set; }

    public static TaskCompletionSource Entered { get; private set; } = new();

    public static void Reset()
    {
        Received.Clear();
        Gate = null;
        Entered = new TaskCompletionSource();
    }

    public async Task HandleAsync(
        ProbeIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        Received.Add(integrationEvent);
        Entered.TrySetResult();

        if (Gate is not null) {
            await Gate.Task;
        }
    }
}
