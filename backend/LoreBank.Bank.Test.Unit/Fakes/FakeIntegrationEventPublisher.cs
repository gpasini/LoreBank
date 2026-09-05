using LoreBank.SharedKernel.Contracts;

namespace LoreBank.Bank.Test.Unit.Fakes;

public sealed class FakeIntegrationEventPublisher : IIntegrationEventPublisher
{
    public List<IIntegrationEvent> Published { get; } = [];

    public Task PublishAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        Published.Add(integrationEvent);

        return Task.CompletedTask;
    }
}
