using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Infrastructure.Signals;

// L'implémentation du socle du port ISignalPolicy (ADR 0026) : tout passe.
// Le socle n'autorise rien (ADR 0023) ; le cloneur qui a des données par
// utilisateur enregistre sa policy dans l'hôte, par-dessus celle-ci.
public sealed class AllowAllSignalPolicy : ISignalPolicy
{
    public bool CanReceive(
        Actor actor,
        Signal signal
    ) => true;
}
