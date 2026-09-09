namespace LoreBank.SharedKernel.Test.Infrastructure.Fakes;

// L'horloge du harnais (ADR 0024) : un test pose l'Instant avant son arrange,
// le fake le rend à toute Application qui demande l'heure ; rien de posé,
// c'est l'horloge système — un test qui n'en parle pas voit le vrai temps.
// Un fake du socle, pas d'un module : tout module qui date un fait en a
// besoin. Singleton mutable non synchronisé, comme les autres fakes —
// l'exécution en série est déclarée par assembly ; ResetFakes l'efface.
public sealed class ConfigurableTimeProvider : TimeProvider
{
    public DateTimeOffset? Instant { get; set; }

    public override DateTimeOffset GetUtcNow() => Instant ?? base.GetUtcNow();

    public void Reset() => Instant = null;
}
