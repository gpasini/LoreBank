using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class RecordingDomainEventHandler : IDomainEventHandler<SomethingHappened>
{
    public int Calls { get; private set; }

    public Task HandleAsync(
        SomethingHappened domainEvent,
        CancellationToken cancellationToken
    )
    {
        Calls++;

        return Task.CompletedTask;
    }
}
