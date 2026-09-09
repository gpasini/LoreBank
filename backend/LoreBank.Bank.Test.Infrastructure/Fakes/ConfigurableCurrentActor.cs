using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Infrastructure.Fakes;

// Le port de l'Acteur, piloté par les tests (ADR 0023) : un test pose
// l'Acteur avant son arrange, BankWebAppFactory.ResetFakes le remet sur
// Anonyme. Singleton mutable non synchronisé, comme les autres fakes —
// l'exécution en série est déclarée par assembly.
public sealed class ConfigurableCurrentActor : ICurrentActor
{
    public Actor Actor { get; set; } = Actor.Anonymous;

    public void Reset() => Actor = Actor.Anonymous;
}
