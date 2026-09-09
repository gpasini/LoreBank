using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class RulingSignalPolicy(Func<Actor, Signal, bool> rule) : ISignalPolicy
{
    public static RulingSignalPolicy AllowAll { get; } = new((
        _,
        _
    ) => true);

    public bool CanReceive(
        Actor actor,
        Signal signal
    ) => rule(
        arg1: actor,
        arg2: signal
    );
}
