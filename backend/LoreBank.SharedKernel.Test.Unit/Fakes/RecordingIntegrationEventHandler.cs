using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class RecordingIntegrationEventHandler : IIntegrationEventHandler<PingIntegrationEvent>
{
    public List<PingIntegrationEvent> Received { get; } = [];

    public Task HandleAsync(
        PingIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        Received.Add(integrationEvent);

        return Task.CompletedTask;
    }
}
