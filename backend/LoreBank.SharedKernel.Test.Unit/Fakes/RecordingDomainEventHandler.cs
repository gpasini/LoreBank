using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class RecordingDomainEventHandler : IDomainEventHandler<SomethingHappenedDomainEvent>
{
    public int Calls { get; private set; }

    public Task HandleAsync(
        SomethingHappenedDomainEvent domainEvent,
        CancellationToken cancellationToken
    )
    {
        Calls++;

        return Task.CompletedTask;
    }
}
