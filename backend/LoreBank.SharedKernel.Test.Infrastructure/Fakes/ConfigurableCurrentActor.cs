using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Infrastructure.Fakes;

// Le port de l'Acteur, piloté par les tests (ADR 0023) : un test pose
// l'Acteur avant son arrange, ResetFakes le remet sur Anonyme. Fourni par
// le harnais du socle à tout hôte de test — comme l'horloge et la policy
// des Signaux —, sauf à celui du socle lui-même, qui garde
// l'implémentation réelle parce qu'il la prouve. Singleton mutable non
// synchronisé, comme les autres fakes : l'exécution en série est déclarée
// par assembly.
public sealed class ConfigurableCurrentActor : ICurrentActor
{
    public Actor Actor { get; set; } = Actor.Anonymous;

    public void Reset() => Actor = Actor.Anonymous;
}
