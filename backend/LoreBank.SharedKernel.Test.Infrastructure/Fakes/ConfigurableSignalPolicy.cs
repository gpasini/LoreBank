using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Infrastructure.Fakes;

// La policy des Signaux du harnais (ADR 0026) : un test pose sa règle avant
// son arrange, le hub la consulte au fan-out ; rien de posé, tout passe —
// le défaut du socle. Un fake du socle, remis à zéro par ResetFakes comme
// l'horloge. Singleton mutable non synchronisé, comme les autres fakes —
// l'exécution en série est déclarée par assembly.
public sealed class ConfigurableSignalPolicy : ISignalPolicy
{
    public Func<Actor, Signal, bool>? Rule { get; set; }

    public bool CanReceive(
        Actor actor,
        Signal signal
    ) => Rule?.Invoke(
        arg1: actor,
        arg2: signal
    ) ?? true;

    public void Reset() => Rule = null;
}
