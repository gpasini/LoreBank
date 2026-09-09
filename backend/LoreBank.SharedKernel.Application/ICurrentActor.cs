using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Application;

// Le seul point où l'identité entre (ADR 0023) : un handler de commande le
// prend en dépendance et passe son Acteur aux transitions d'agrégat qui
// enregistrent leur auteur. Anonyme tant que personne n'authentifie ; le
// jour où le cloneur monte son schéma, l'implémentation du socle lit
// l'identifiant du principal — rien à changer dans les modules.
public interface ICurrentActor
{
    Actor Actor { get; }
}
