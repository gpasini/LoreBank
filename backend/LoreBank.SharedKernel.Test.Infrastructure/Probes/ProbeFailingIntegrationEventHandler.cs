using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Échoue tant qu'on le lui demande : la sonde des retries, du backoff et du
// marquage poison. Second handler du même event que la sonde enregistreuse —
// c'est le couple qui prouve la transaction par handler.
public sealed class ProbeFailingIntegrationEventHandler : IIntegrationEventHandler<ProbeIntegrationEvent>
{
    public static bool ShouldFail { get; set; } = true;

    public static int Invocations { get; private set; }

    public static void Reset()
    {
        ShouldFail = true;
        Invocations = 0;
    }

    public Task HandleAsync(
        ProbeIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        Invocations++;

        return ShouldFail
            ? throw new InvalidOperationException("Échec volontaire de la sonde.")
            : Task.CompletedTask;
    }
}
