using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Application.Signals;

// L'endroit exact où décider qui reçoit quoi (ADR 0026) : le socle
// n'autorise rien (ADR 0023), il consulte ce port au fan-out avec l'Acteur
// de la connexion. L'implémentation du socle laisse tout passer ; le
// cloneur qui a des données par utilisateur enregistre la sienne dans
// l'hôte — les modules n'ont rien à changer.
public interface ISignalPolicy
{
    bool CanReceive(
        Actor actor,
        Signal signal
    );
}
